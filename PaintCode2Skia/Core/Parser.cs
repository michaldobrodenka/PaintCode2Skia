using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PaintCode2Skia.Core
{
    public class Parser
    {
        private static readonly HashSet<string> classesInTemplate = new HashSet<string>
        {
            { "PaintCodeColor" },
            { "PaintCodeGradient" },
            { "PaintCodeLinearGradient" },
            { "PaintCodeRadialGradient" },
            { "PaintCodeStaticLayout" },
        };

        private static readonly HashSet<string> methodsInHelpers = new HashSet<string>()
        {
            {"resizingBehaviorApply"},
        };

        private static readonly Dictionary<string, string> dataTypesMap = new Dictionary<string, string>()
        {
            { "Paint", "SKPaint" },
            { "TextPaint", "SKPaint" }, // the SKFont half travels alongside, see TextPaintsInMethod
            { "Canvas", "SKCanvas" },
            { "RectF", "SKRect" },
            { "boolean", "bool" },
            { "PointF", "SKPoint" },
            { "Path", "SKPath" }, // a path taken as a parameter is finished geometry, not a builder
            //{ "Context", "IFontProvider" }
        };

        private static readonly Dictionary<string, string> gettersMap = new Dictionary<string, string>()
        {
            { ".width()", ".Width" },
            { ".height()", ".Height" },
            {".centerX()", ".MidX" },
            {".centerY()", ".MidY" },
        };

        private static readonly Dictionary<string, string> simpleCommandsMap = new Dictionary<string, string>()
        {
            { "canvas.save", "canvas.Save" },
            { "canvas.restore", "canvas.Restore" },
            { "canvas.translate", "canvas.Translate" },
            { "canvas.scale", "canvas.Scale" },
            { "canvas.rotate", "canvas.RotateDegrees" },
            { "canvas.clipRect", "canvas.ClipRect" },
            { "Path.reset", "Path.Reset" },
            { "Path.moveTo", "Path.MoveTo" },
            { "Path.lineTo", "Path.LineTo" },
            {"canvas.SaveLayer(null, paint, Canvas.ALL_SAVE_FLAG);" , "canvas.SaveLayer(paint);" },
            {"aint.reset()", "aint.Reset()" },
            { "canvas.drawColor", "canvas.DrawColor" },
            { "canvas.drawPath", "canvas.DrawPath" },
            { "canvas.drawPaint", "canvas.DrawPaint" },
            {".setFlags(Paint.ANTI_ALIAS_FLAG);", ".IsAntialias = true;" },
            {".setStyle(Paint.Style.STROKE)", ".Style = SKPaintStyle.Stroke" },
            {".setStyle(Paint.Style.STROKE_AND_FILL)", ".Style = SKPaintStyle.StrokeAndFill" },
            {".setStyle(Paint.Style.FILL)", ".Style = SKPaintStyle.Fill" },
            {"Path.addRect", "Path.AddRect" },
            {"Path.addArc", "Path.AddArc" },
            {"Path.addRoundRect","Path.AddRoundRect" },
            {"s.clipPath", "s.ClipPath" },
            {"Path.addOval", "Path.AddOval" },
            {"Path.close()", "Path.Close()" },
            {"new RectF", "new SKRect" },
            {"new PointF", "new SKPoint" },
            {".left", ".Left" },
            {".right", ".Right" },
            {".top", ".Top" },
            {".y", ".Y" },
            {".x", ".X" },
            {".bottom", ".Bottom" },
            {"Path.Direction.CW" , "SKPathDirection.Clockwise" },
            {"Path.Direction.CCW" , "SKPathDirection.CounterClockwise" },
            {"Math.sin", "Math.Sin" },
            {"Math.cos", "Math.Cos" },
            { "Math.min", "Math.Min" },
            {"Math.max", "Math.Max" },
            {"Math.abs", "Math.Abs" },
            {"Math.ceil", "Math.Ceiling" },
            {"Math.round", "Math.Round" },
            {"Math.floor", "Math.Floor" },
            {"Path.cubicTo", "Path.CubicTo" },
            {"Frame.left", "Frame.Left" },
            {"Frame.right", "Frame.Right" },
            {"Frame.top", "Frame.Top" },
            {"Frame.bottom", "Frame.Bottom" },
            {".setFillType(Path.FillType.EVEN_ODD)", ".FillType = SKPathFillType.EvenOdd" },
            {"boolean ", "bool " },
            {"setStrokeJoin(Paint.Join.ROUND)", "StrokeJoin = SKStrokeJoin.Round" },
            {"setStrokeJoin(Paint.Join.BEVEL)", "StrokeJoin = SKStrokeJoin.Bevel" },
            {"setStrokeCap(Paint.Cap.ROUND)", "StrokeCap = SKStrokeCap.Round" },
            {"setStrokeCap(Paint.Cap.SQUARE)", "StrokeCap = SKStrokeCap.Square" },
            {"setStrokeCap(Paint.Cap.BUTT)", "StrokeCap = SKStrokeCap.Butt" },
            { "Color.BLACK", "Helpers.ColorBlack" },
            {"Color.WHITE", "Helpers.ColorWhite" },
            {"Color.GRAY", "Helpers.ColorGray" },
            {"Color.RED", "Helpers.ColorRed" },
            {"Color.GREEN", "Helpers.ColorGreen" },
            {"Color.LTGRAY", "Helpers.ColorLightGray" },
            {".setXfermode(GlobalCache.blendModeMultiply)", ".BlendMode = SKBlendMode.Multiply" },
            {".setXfermode(GlobalCache.blendModeDestinationOut)", ".BlendMode = SKBlendMode.DstOut;" },
            {".setXfermode(GlobalCache.blendModeSourceIn);", ".BlendMode = SKBlendMode.SrcIn;" },
            {"Layout.Alignment.ALIGN_CENTER", "SKTextAlign.Center"},
            {"Layout.Alignment.ALIGN_NORMAL", "SKTextAlign.Left"},
            {"Layout.Alignment.ALIGN_OPPOSITE", "SKTextAlign.Right"},
            {"String.valueOf(", "Helpers.StringValueOf(" },
            {".postRotate(", " = SKMatrix.CreateRotationDegrees(" },
            {".transform(",".Transform(in "}, // SKPath.Transform(SKMatrix) by value is an error in SkiaSharp 4
            {".invert(",".TryInvert(out " },
            {"(int) (Color.alpha", "(byte) (Color.alpha" },
            //{".computeBounds",  }
        };


        private enum FilePart
        {
            Start,
            Class,
            Enum,
            CacheClass,
            GlobalCacheClass,
            Method,
            AfterMainClass,
            InAuxClass
        }

        /// <summary>
        /// SkiaSharp 4 builds geometry with SKPathBuilder but draws SKPath, so every path variable
        /// lives in two halves: the cached builder it is constructed through, and the SKPath that
        /// Detach() hands over once the shape is finished.
        /// </summary>
        private class PathVar
        {
            public string BuilderName;

            public bool Declared;    // "SKPath x = ..." has already been emitted once

            public bool NeedsDetach; // the builder holds geometry that has not been handed over yet

            // Almost every PaintCode path is pure geometry: the resizing is applied to the canvas,
            // not baked into the coordinates, so the shape is the same on every call. Such a path is
            // built once and kept, which removes the rebuild and the per-draw SKPath allocation.
            public bool Constant = true;

            public string CacheSource;  // "CacheForY.xPath", or null for a path made with new Path()

            public List<string> BuildLines = new List<string>();

            public string Indent = "        ";
        }

        // Everything that mutates a path. These are obsolete on SKPath in SkiaSharp 4, so they have
        // to go to the builder; anything else (Transform, ComputeTightBounds, DrawPath, ClipPath) is
        // ordinary non-obsolete SKPath API and runs on the detached path.
        private static readonly HashSet<string> pathBuildMembers = new HashSet<string>()
        {
            "Reset", "Rewind", "MoveTo", "LineTo", "CubicTo", "QuadTo", "ConicTo", "ArcTo", "Close",
            "RMoveTo", "RLineTo", "RCubicTo", "RQuadTo", "RConicTo", "RArcTo",
            "AddRect", "AddOval", "AddArc", "AddCircle", "AddPoly", "AddPath", "AddRoundRect",
            "FillType",
        };

        // An identifier that is not preceded by a digit, dot or word character, so the "f" of "3.5f"
        // contributes nothing and "SKPathDirection.Clockwise" contributes only "SKPathDirection".
        private static readonly Regex identifierRegex = new Regex(@"(?<![A-Za-z0-9_.])[A-Za-z_][A-Za-z0-9_]*");

        // Identifiers that never make an expression vary between calls. Math is here because its
        // arguments are tokenised separately, so Math.Min(a, b) is constant exactly when a and b are.
        private static readonly HashSet<string> alwaysConstantIdentifiers = new HashSet<string>()
        {
            "new", "true", "false", "null", "Math", "var", "float", "int", "bool", "double",
        };

        // Where each cached path was declared in the output, so the declaration can be rewritten from
        // SKPathBuilder to SKPath once the method body has shown the geometry to be constant.
        private readonly Dictionary<string, int> pathCacheLineIndex = new Dictionary<string, int>();

        private string currentCacheClassName;

        /// <summary>
        /// True when every identifier in the expression is a literal, an SkiaSharp type or enum, or a
        /// local already known to be constant. Anything unrecognised counts as varying, so a path is
        /// only ever cached when it is provably safe: a false negative costs a rebuild, a false
        /// positive would freeze a shape that is supposed to move.
        /// </summary>
        private bool IsConstantExpression(string expression)
        {
            foreach (Match match in identifierRegex.Matches(expression ?? String.Empty))
            {
                var identifier = match.Value;

                if (alwaysConstantIdentifiers.Contains(identifier))
                    continue;

                if (identifier.StartsWith("SK"))
                    continue;

                if (this.currentContext.ConstLocalsInMethod.Contains(identifier))
                    continue;

                return false;
            }

            return true;
        }

        /// <summary>
        /// The part of a builder call that carries geometry: the arguments of "AddOval(rect, dir);"
        /// or the right hand side of "FillType = SKPathFillType.EvenOdd;".
        /// </summary>
        private static string ArgumentsOf(string call)
        {
            var bracket = call.IndexOf('(');

            if (bracket >= 0)
            {
                var close = call.LastIndexOf(')');
                return close > bracket ? call.Substring(bracket + 1, close - bracket - 1) : call.Substring(bracket + 1);
            }

            var equals = call.IndexOf('=');

            return equals >= 0 ? call.Substring(equals + 1) : String.Empty;
        }

        /// <summary>
        /// Records whether a local declared in the method body is constant, so that later path
        /// geometry built from it can be classified.
        /// </summary>
        private void TrackConstLocal(string trimmedLine)
        {
            var equals = trimmedLine.IndexOf('=');

            if (equals < 0 || trimmedLine.Contains("(") && trimmedLine.IndexOf('(') < equals)
                return;

            var declaration = trimmedLine.Substring(0, equals).Trim();
            var words = declaration.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            if (words.Length != 2)
                return;

            var name = words[1];
            var value = trimmedLine.Substring(equals + 1).TrimEnd(';', ' ', '\t');

            if (IsConstantExpression(value))
                this.currentContext.ConstLocalsInMethod.Add(name);
            else
                this.currentContext.ConstLocalsInMethod.Remove(name);
        }

        /// <summary>
        /// A PaintCode TextPaint, which in SkiaSharp 4 is an SKPaint plus the SKFont carrying the
        /// typeface and size.
        /// </summary>
        private class TextPaintVar
        {
            public string FontName;

            public string FontSource; // the cache expression the font is fetched from

            public bool FontDeclared;
        }

        private class Context
        {
            public FilePart FilePart { get; set; }

            public string CurrentClassName { get; set; }

            public String CurrentNestedClassName { get; set; }

            public int ExtraNestingInMethod { get; set; }

            public string CurrentMethodName { get; set; }


            // for cases like:
            // java:
            //      RectF circletransparentRect = CacheForRectanglestroke2.circletransparentRect;
            //      circletransparentRect.set(8f, 6f, 23f, 21f);
            // we skip first line. We do not cache RectF objects (they are simple structs)
            public bool SkippingRectFfromCache { get; set; }

            public bool OmitCurrentEnum { get; set; }

            public bool OmitCurrentMethod { get; set; }

            public bool OmitCurrentAuxClass { get; set; }

            public bool LastLineWasNewLine { get; set; }

            public bool NeedToReplaceTwoBrackets { get; set; } // )); could not be on the same line as beginnig of the command

            public string CurrentMethodNameAndModifiers { get; set; }

            public List<Tuple<string, string>> CurrentMethodParameters { get; set; } = new List<Tuple<string, string>>();

            public List<string> CurrentMethodLines { get; set; } = new List<string>();

            public List<Tuple<int, string>> DisposablesInCurrentMethodAtNesting = new List<Tuple<int, string>>();

            public int CurrentTempPaintsForSaveLayerAlpha = 0;

            public Dictionary<string, PathVar> PathsInMethod { get; } = new Dictionary<string, PathVar>();

            // Locals whose value is the same on every call, so a path built from them is constant.
            public HashSet<string> ConstLocalsInMethod { get; } = new HashSet<string>();

            public Dictionary<string, TextPaintVar> TextPaintsInMethod { get; } = new Dictionary<string, TextPaintVar>();
        }

        private struct OutRectInfo
        {
            public string Type;
            public string Name;

            public OutRectInfo(string type, string name)
            {
                Type = type;
                Name = name;
            }
        }

        private Dictionary<string, List<OutRectInfo>> methodsWithAddedOutRect = new Dictionary<string, List<OutRectInfo>>();

        string csNamespace;

        Context currentContext;

        List<string> output = new List<string>();

        public Parser()
        {
            this.currentContext = new Context()
            {
                FilePart = FilePart.Start,
            };
        }

        public string[] ParsePaintCodeJavaCode(string[] javaLines, string csNamespace)
        {
            this.csNamespace = csNamespace;

            this.output.Add(String.Format(global::PaintCode2Skia.Resources.Resources.Header, this.csNamespace));

            for (int i = 0; i < javaLines.Length; i++)
            {
                var line = javaLines[i];

                var trimmedLine = line.Trim();

                if (String.IsNullOrEmpty(trimmedLine))
                {
                    if (!this.currentContext.LastLineWasNewLine)
                    {
                        if (this.currentContext.FilePart == FilePart.Method)
                        {
                            this.currentContext.CurrentMethodLines.Add(line);
                        }
                        else
                        {
                            this.output.Add(line);
                        }
                    }

                    this.currentContext.LastLineWasNewLine = true;
                    continue;
                }

                if (trimmedLine.StartsWith("//"))
                {
                    if (this.currentContext.FilePart == FilePart.Method)
                    {
                        this.currentContext.CurrentMethodLines.Add(line);
                    }
                    else
                    {
                        this.output.Add(line);
                    }

                    continue;
                }

                this.currentContext.LastLineWasNewLine = false;

                switch (this.currentContext.FilePart)
                {
                    case FilePart.Start:
                        this.ParseLineInFileStart(trimmedLine, line);
                        break;

                    case FilePart.Class:
                        this.ParseLineInMainClass(trimmedLine, line);
                        break;

                    case FilePart.Enum:
                        this.ParseLineInEnum(trimmedLine, line);
                        break;

                    case FilePart.CacheClass:
                        this.ParseLineInCacheClass(trimmedLine, line);
                        break;

                    case FilePart.GlobalCacheClass:
                        this.ParseLineInGlobalCacheClass(trimmedLine, line);
                        break;

                    case FilePart.Method:
                        this.ParseLineInMethod(trimmedLine, line, javaLines, i);
                        break;

                    case FilePart.AfterMainClass:
                        this.ParseLineAfterMainClass(trimmedLine, line);
                        break;

                    case FilePart.InAuxClass:
                        this.ParseLineInAuxClass(trimmedLine, line);
                        break;
                }

                //this.ParseLine(line.Trim());
            }

            AddOutRectsToMethods();

            this.output.Add("} // end of namespace");

            return this.output.ToArray();
        }

        private void ParseLineInAuxClass(string trimmedLine, string line)
        {
            if (trimmedLine.EndsWith("{"))
            {
                this.currentContext.ExtraNestingInMethod++;
            }
            else if (trimmedLine == "}")
            {
                this.currentContext.ExtraNestingInMethod--;

                if (this.currentContext.ExtraNestingInMethod == 0)
                    this.currentContext.FilePart = FilePart.AfterMainClass;
            }
        }

        private void ParseLineAfterMainClass(string trimmedLine, string line)
        {
            if (trimmedLine.Contains("class"))
            {
                var components = trimmedLine.Split(' ');

                string className;
                className = components[1];

                if (classesInTemplate.Contains(className))
                {
                    this.currentContext.OmitCurrentMethod = true;
                }

                this.currentContext.FilePart = FilePart.InAuxClass;
            }
        }

        private void ParseLineInGlobalCacheClass(string trimmedLine, string line)
        {
            if (trimmedLine == "}")
            {
                this.currentContext.FilePart = FilePart.Class;
                this.output.Add(line);
                return;
            }

            var words = trimmedLine.Split(new char[] { ' ', '\t' });

            string type;
            string name;

            if (words[0] == "private" || words[0] == "public")
            {
                type = words[2];
                name = words[3];
            }
            else
            {
                type = words[1];
                name = words[2];
            }

            switch (type)
            {
                case "PorterDuffXfermode":
                    Console.WriteLine("INFO: PorterDuffXfermode will not be cached in global cache");
                    break;

                default:
                    Console.WriteLine("ERROR: Unknon data type in cache class:" + type);
                    break;
            }
        }

        private void ParseLineInMethod(string trimmedLine, string line, string[] lines, int currentLineIndex)
        {
            if (trimmedLine.EndsWith("{"))
            {
                this.currentContext.ExtraNestingInMethod++;
            }
            else if (trimmedLine == "}")
            {
                if (this.currentContext.ExtraNestingInMethod > 0)
                {
                    foreach (var disposable in this.currentContext.DisposablesInCurrentMethodAtNesting.ToArray())
                    {
                        if (disposable.Item1 == this.currentContext.ExtraNestingInMethod)
                        {
                            this.currentContext.CurrentMethodLines.Add("            " + disposable.Item2 + ".Dispose();");

                            this.currentContext.DisposablesInCurrentMethodAtNesting.Remove(disposable);
                        }
                    }


                    this.currentContext.ExtraNestingInMethod--;


                    //this.currentContext.CurrentMethodLines

                }
                else
                {
                    if (!this.currentContext.OmitCurrentMethod)
                    {
                        // flush method to output
                        var signature = this.currentContext.CurrentMethodNameAndModifiers;

                        for (int i = 0; i < this.currentContext.CurrentMethodParameters.Count; i++)
                        {
                            var parameter = this.currentContext.CurrentMethodParameters[i];
                            signature += parameter.Item1 + " " + parameter.Item2;

                            if (i < this.currentContext.CurrentMethodParameters.Count - 1)
                            {
                                signature += ", ";
                            }
                        }

                        signature += ")";

                        // Anything still registered was made at method-body level, so this is the
                        // last point at which it can be handed back.
                        foreach (var disposable in this.currentContext.DisposablesInCurrentMethodAtNesting)
                            this.currentContext.CurrentMethodLines.Add("        " + disposable.Item2 + ".Dispose();");

                        this.currentContext.DisposablesInCurrentMethodAtNesting.Clear();

                        this.output.Add(signature);
                        this.output.AddRange(this.currentContext.CurrentMethodLines);
                        this.output.Add("    }");
                    }

                    this.currentContext.FilePart = FilePart.Class;
                }
            }


            if (!this.currentContext.OmitCurrentMethod)
            {
                if (this.currentContext.NeedToReplaceTwoBrackets && trimmedLine.EndsWith(";"))
                {
                    line = line.Replace("));", ");");
                    trimmedLine = trimmedLine.Replace("));", ");");
                    this.currentContext.NeedToReplaceTwoBrackets = false;
                }

                if (trimmedLine.EndsWith("{"))
                {
                    this.currentContext.CurrentMethodLines.Add(line);
                }
                else if (trimmedLine.StartsWith("//"))
                {
                    this.currentContext.CurrentMethodLines.Add(line);
                }
                else if (trimmedLine.StartsWith("Paint "))
                {
                    if (NextLine(lines, currentLineIndex)?.Contains("aint.set(") ?? false)
                    {
                        // skip, we will use paint.Clone() and local variable with Dispose()
                    }
                    else
                    {
                        this.currentContext.CurrentMethodLines.Add(line.ReplaceFirst("Paint ", "SKPaint "));
                    }
                }
                else if (trimmedLine.Contains("anvas.concat("))
                {
                    this.currentContext.CurrentMethodLines.Add("// " + line + " // not supported yet");
                }
                else if (trimmedLine.Contains("ransformation.pop()"))
                {
                    this.currentContext.CurrentMethodLines.Add("// " + line + " // not supported yet");
                }
                else if (trimmedLine.Contains("aint.set(") && trimmedLine.Contains("aint);"))
                {
                    line = "            var " + trimmedLine.Replace(".set(", " = ");
                    line = line.Replace(");", ".Clone();");
                    this.currentContext.CurrentMethodLines.Add(line);
                    this.currentContext.DisposablesInCurrentMethodAtNesting.Add(new Tuple<int, string>(this.currentContext.ExtraNestingInMethod, trimmedLine.Split('.')[0]));
                }
                else if (trimmedLine.StartsWith("Path "))
                {
                    // "Path xPath = CacheForY.xPath;" -> the cache holds the builder, so the local
                    // becomes xPathBuilder; the SKPath named xPath appears at the first use.
                    var name = trimmedLine.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)[1];
                    var builderName = name + "Builder";

                    // The source of the path, either a cache slot or a fresh "new Path()".
                    var source = trimmedLine.Substring(trimmedLine.IndexOf('=') + 1).Trim().TrimEnd(';').Trim();

                    if (this.currentContext.PathsInMethod.TryGetValue(name, out var existing))
                    {
                        // re-assigned later in the same method - start a second shape under the same name
                        existing.NeedsDetach = false;
                        existing.Constant = false; // two shapes in one call cannot both be the cached one
                        existing.BuildLines.Clear();
                    }
                    else
                    {
                        this.currentContext.PathsInMethod[name] = new PathVar()
                        {
                            BuilderName = builderName,
                            CacheSource = source.StartsWith("Cache") ? source : null,
                            Constant = source.StartsWith("Cache"),
                            Indent = line.Substring(0, line.Length - line.TrimStart().Length),
                        };
                    }

                    // Nothing is emitted here. The build calls are buffered and written out at the
                    // point the finished path is first used, in whichever of the two shapes applies.
                }
                else if (trimmedLine.StartsWith("int ") && (trimmedLine.Contains(" = Color.argb") || trimmedLine.ToLower().Contains("color")))
                {
                    this.currentContext.CurrentMethodLines.Add(line.Replace("int ", "SKColor ")
                        .Replace(" = Color.argb", " = Helpers.ColorFromArgb").Replace("(int)", "(byte)"));
                }
                else if (trimmedLine.Contains(".resizingBehaviorApply("))
                {
                    this.currentContext.CurrentMethodLines.Add("        var resizedFrame = " + trimmedLine.Replace(", resizedFrame);", ");").Replace(this.currentContext.CurrentClassName + ".resizingBehaviorApply", "Helpers.ResizingBehaviorApply"));
                    this.currentContext.SkippingRectFfromCache = false;
                }
                else if (trimmedLine.StartsWith("RectF ") && trimmedLine.Contains("= Cache"))
                {
                    ///*this.currentContext.SkippingRectFfromCache*/ = trimmedLine.Split(' ')[1];
                    this.currentContext.SkippingRectFfromCache = true;
                    return; // skipping - we are not caching these rects
                }
                else if (trimmedLine.Contains(".set(") && this.currentContext.SkippingRectFfromCache)
                {
                    var rectName = trimmedLine.Split('.')[0];

                    var rectLine = ("        var " + rectName + " = new SKRect" + trimmedLine.Remove(0, trimmedLine.IndexOf('('))).ReplaceAll(gettersMap).ReplaceAll(simpleCommandsMap);

                    // These rects carry most of the path geometry, so whether they are constant
                    // decides whether the paths built from them can be cached.
                    this.TrackConstLocal(rectLine.Trim());

                    this.currentContext.CurrentMethodLines.Add(rectLine);
                    //this.currentContext.CurrentMethodLines.Add("        var " + trimmedLine.Split('.')[0] + " = new SKRect(" + trimmedLine.Split('(')[1]);

                    if (rectName.StartsWith("embed"))
                    {
                        var embedRectName = rectName.ReplaceFirst("embed", String.Empty);
                        embedRectName = embedRectName.ReplaceFirst(embedRectName[0].ToString(), embedRectName[0].ToString().ToLower());
                        this.currentContext.CurrentMethodParameters.Add(new Tuple<string, string>("out SKRect", embedRectName));
                        this.currentContext.CurrentMethodLines.Add("        " + embedRectName + " = " + rectName + "; // set SKRect for use outside");

                        if (!methodsWithAddedOutRect.ContainsKey(this.currentContext.CurrentMethodName))
                        {
                            this.methodsWithAddedOutRect.Add(this.currentContext.CurrentMethodName, new List<OutRectInfo>());
                        }

                        this.methodsWithAddedOutRect[this.currentContext.CurrentMethodName].Add(new OutRectInfo("out SKRect", embedRectName));
                    }

                    this.currentContext.SkippingRectFfromCache = false;
                }
                else if (trimmedLine.Contains(".setTypeface(Typeface.createFromAsset(context.getAssets(), "))
                {
                    this.AddTextFontLine(line.Replace(".setTypeface(Typeface.createFromAsset(context.getAssets(), ", ".Typeface = TypefaceManager.GetTypeface(").Replace("));", ");"), trimmedLine);
                }
                else if (trimmedLine.Contains(" new PaintCodeGradient") && trimmedLine.Contains("new int[]"))
                {
                    this.currentContext.CurrentMethodLines.Add(line.Replace("new int[]", "new SKColor[]").ReplaceAll(simpleCommandsMap).ReplaceAll(gettersMap));
                }
                else if (trimmedLine.Contains("TextPaint "))
                {
                    // "TextPaint xTextPaint = CacheForY.xTextPaint;" - remember where the matching
                    // SKFont comes from; it is declared lazily at the first typeface/size assignment.
                    var name = trimmedLine.ReplaceFirst("TextPaint ", String.Empty).Split(new char[] { ' ', '\t', '=' }, StringSplitOptions.RemoveEmptyEntries)[0];
                    var fontName = FontNameFor(name);

                    var equals = trimmedLine.IndexOf('=');
                    var source = equals < 0 ? String.Empty : trimmedLine.Substring(equals + 1).Trim().TrimEnd(';').Trim();

                    this.currentContext.TextPaintsInMethod[name] = new TextPaintVar()
                    {
                        FontName = fontName,
                        FontSource = source.Contains("new ") ? "new SKFont()" : source.ReplaceFirst(name, fontName),
                    };

                    this.currentContext.CurrentMethodLines.Add(line.ReplaceFirst("TextPaint ", "var "));
                }
                else if (trimmedLine.Contains(".colorByChangingAlpha("))
                {
                    var newLine = line.Replace(".setShader(", ".Shader = ").ReplaceAll(simpleCommandsMap).ReplaceAll(gettersMap);

                    if (!line.Contains("));"))
                        this.currentContext.NeedToReplaceTwoBrackets = true;

                    this.currentContext.CurrentMethodLines.Add(newLine.Replace("));", ");"));
                }
                else if (trimmedLine.Contains(".setShader("))
                {
                    var newLine = line.Replace(".setShader(", ".Shader = ").ReplaceAll(simpleCommandsMap).ReplaceAll(gettersMap);

                    if (!line.Contains("));"))
                        this.currentContext.NeedToReplaceTwoBrackets = true;

                    this.currentContext.CurrentMethodLines.Add(newLine.Replace("));", ");"));
                }
                else if (trimmedLine.Contains(".setPathEffect("))
                {
                    var newLine = line.Replace(".setPathEffect(", ".PathEffect = ").ReplaceAll(simpleCommandsMap).ReplaceAll(gettersMap);

                    if (!line.Contains("));"))
                        this.currentContext.NeedToReplaceTwoBrackets = true;

                    this.currentContext.CurrentMethodLines.Add(newLine.Replace("));", ");"));
                }
                else if (trimmedLine.Contains(".setTextSize("))
                {
                    // on an SKFont the property is Size; SKPaint.TextSize is obsolete in SkiaSharp 4
                    var isFont = this.currentContext.TextPaintsInMethod.ContainsKey(trimmedLine.Split('.')[0]);

                    this.AddTextFontLine(line.ReplaceFirst(".setTextSize(", isFont ? ".Size = " : ".TextSize = ").Replace(")", ""), trimmedLine);
                }
                else if (trimmedLine.Contains(".setStrokeWidth("))
                {
                    this.currentContext.CurrentMethodLines.Add(line.ReplaceFirst(".setStrokeWidth(", ".StrokeWidth = ").Replace(")", ""));
                }
                else if (trimmedLine.Contains(".setStrokeMiter("))
                {
                    this.currentContext.CurrentMethodLines.Add(line.ReplaceFirst(".setStrokeMiter(", ".StrokeMiter = ").Replace(")", ""));
                }
                else if (trimmedLine.Contains(".setColor("))
                {
                    this.currentContext.CurrentMethodLines.Add(line.ReplaceFirst(".setColor(", ".Color = (SKColor)").Replace(");", ";").ReplaceAll(simpleCommandsMap).ReplaceAll(gettersMap));
                }
                else if (trimmedLine.Contains("canvas.saveLayerAlpha("))
                {
                    var firstBracketIndex = line.IndexOf('(');
                    var parametersStr = line.Remove(0, firstBracketIndex + 1);
                    var rect = parametersStr.Split(',')[0];
                    var value = parametersStr.Split(',')[1].Trim();
                    value = value.Replace("(int)", "(byte)");

                    this.currentContext.CurrentTempPaintsForSaveLayerAlpha++;
                    var tempPaintName = "tempPaint" + this.currentContext.CurrentTempPaintsForSaveLayerAlpha;

                    var tempPaintLine = $"            var {tempPaintName} = Helpers.PaintWithAlpha({value});";
                    this.currentContext.CurrentMethodLines.Add(tempPaintLine);

                    var newLine = $"        canvas.SaveLayer(";
                    if (!string.IsNullOrEmpty(rect) && rect != "null")
                    {
                        newLine += rect + ", ";
                    }
                    newLine += tempPaintName + ");";
                    this.currentContext.CurrentMethodLines.Add(newLine);

                    this.currentContext.DisposablesInCurrentMethodAtNesting.Add(new Tuple<int, string>(this.currentContext.ExtraNestingInMethod, tempPaintName));
                }
                else if (trimmedLine.Contains("canvas.saveLayer(null, ") && trimmedLine.Contains(", Canvas.ALL_SAVE_FLAG);"))
                {
                    var newLine = trimmedLine.Replace("(null, ", "(");
                    newLine = newLine.Replace(".saveLayer", ".SaveLayer");
                    newLine = newLine.Replace(", Canvas.ALL_SAVE_FLAG);", ");");
                    newLine = "        " + newLine;
                    this.currentContext.CurrentMethodLines.Add(newLine);
                }
                else if (trimmedLine.Contains(".computeBounds("))
                {
                    var firstBracketIndex = line.IndexOf('(');
                    var parametersStr = line.Remove(0, firstBracketIndex + 1);
                    var bounds = parametersStr.Split(',')[0];
                    var path = trimmedLine.Split('.')[0];
                    var newLine = "        var " + bounds + " = " + path + ".ComputeTightBounds();";
                    this.AddMethodLine(newLine);
                }
                else if (trimmedLine.Contains("Matrix"))
                {
                    if (trimmedLine.Contains("Stack<Matrix>") || trimmedLine.Contains(".push"))
                    {
                        this.currentContext.CurrentMethodLines.Add("// " + line + " // skipping - we do not support Matrix yet");
                    }
                    else
                    {
                        this.currentContext.CurrentMethodLines.Add(line.Replace("Matrix", "SKMatrix").Replace(" = ", "; //"));
                    }
                }
                else if (trimmedLine.StartsWith("RectF") && trimmedLine.Contains("new RectF"))
                {
                    this.currentContext.CurrentMethodLines.Add(trimmedLine.Replace("RectF", "SKRect"));
                }
                else if (trimmedLine.Contains("Transformation.peek()"))
                {
                    this.currentContext.CurrentMethodLines.Add("// " + line + " // skipping - we do not support Matrix yet");
                }
                else if (trimmedLine.Contains("StaticLayout ") && trimmedLine.Contains(".get("))
                {
                    // the layout needs the SKFont alongside the SKPaint now
                    var newLine = line.ReplaceAll(simpleCommandsMap).ReplaceAll(gettersMap);

                    foreach (var textPaint in this.currentContext.TextPaintsInMethod)
                    {
                        var tail = textPaint.Key + ");";

                        if (newLine.TrimEnd().EndsWith(tail))
                        {
                            newLine = newLine.ReplaceFirst(tail, textPaint.Key + ", " + textPaint.Value.FontName + ");");
                            break;
                        }
                    }

                    this.AddMethodLine(newLine);
                }
                else if (trimmedLine == "}")
                {
                    this.currentContext.NeedToReplaceTwoBrackets = false;
                    this.currentContext.CurrentMethodLines.Add(line);
                }
                else
                {
                    this.AddMethodLine(line.ReplaceAll(simpleCommandsMap).ReplaceAll(gettersMap));
                }
            }
        }

        /// <summary>
        /// Adds a translated line, keeping the builder/path split straight: a construction call is
        /// retargeted at the cached SKPathBuilder, and the first time a finished shape is used for
        /// anything else it is detached into a real SKPath under the original variable name.
        /// </summary>
        private void AddMethodLine(string line)
        {
            this.TrackConstLocal(line.Trim());

            var paths = this.currentContext.PathsInMethod;

            if (paths.Count == 0)
            {
                this.currentContext.CurrentMethodLines.Add(line);
                return;
            }

            var trimmed = line.TrimStart();
            var indent = line.Substring(0, line.Length - trimmed.Length);

            // Is this line building one of the tracked paths?
            string buildReceiver = null;
            var dot = trimmed.IndexOf('.');

            if (dot > 0)
            {
                var receiver = trimmed.Substring(0, dot);

                if (paths.ContainsKey(receiver) && pathBuildMembers.Contains(MemberName(trimmed.Substring(dot + 1))))
                    buildReceiver = receiver;
            }

            // Every other path mentioned here is finished and has to become an SKPath first.
            foreach (var path in paths)
            {
                if (path.Key == buildReceiver || !path.Value.NeedsDetach || !ContainsWord(line, path.Key))
                    continue;

                EmitFinishedPath(path.Key, path.Value, indent);
            }

            if (buildReceiver != null)
            {
                var pathVar = paths[buildReceiver];
                pathVar.NeedsDetach = true;

                var call = trimmed.Substring(dot + 1);

                // Constant geometry is the whole point of caching the built path, so one varying
                // argument anywhere in the shape disqualifies it. Only the arguments are tested -
                // the member name is part of the call, not of the geometry.
                if (!IsConstantExpression(ArgumentsOf(call)))
                    pathVar.Constant = false;

                pathVar.BuildLines.Add(call);
                return;
            }

            this.currentContext.CurrentMethodLines.Add(line);
        }

        /// <summary>
        /// Writes out a path whose geometry is complete, in one of two shapes.
        ///
        /// Constant geometry - which is almost all of it, because PaintCode applies the resizing to
        /// the canvas rather than to the coordinates - is built once and kept in the cache slot, so
        /// repeat draws do no path work at all. Geometry that depends on the arguments is rebuilt
        /// through the cached builder as before, and the SKPath that Detach() hands over is disposed
        /// at the end of the block it was made in.
        /// </summary>
        private void EmitFinishedPath(string name, PathVar path, string indent)
        {
            var lines = this.currentContext.CurrentMethodLines;

            if (path.Constant && path.CacheSource != null && !path.Declared)
            {
                RewritePathCacheDeclaration(path.CacheSource);

                lines.Add(indent + "SKPath " + name + " = " + path.CacheSource + ";");
                lines.Add(indent + "if (" + name + " == null) {");
                lines.Add(indent + "    var " + path.BuilderName + " = new SKPathBuilder();");

                foreach (var build in path.BuildLines)
                {
                    // A fresh builder needs no clearing, and Reset() would also drop the fill type.
                    if (build == "Reset();" || build == "Rewind();")
                        continue;

                    lines.Add(indent + "    " + path.BuilderName + "." + build);
                }

                lines.Add(indent + "    " + name + " = " + path.CacheSource + " = " + path.BuilderName + ".Detach();");
                lines.Add(indent + "    " + path.BuilderName + ".Dispose();");
                lines.Add(indent + "}");
            }
            else
            {
                // The shape changes from call to call, so it has to be rebuilt every time.
                lines.Add(indent + (path.Declared ? path.BuilderName : "SKPathBuilder " + path.BuilderName)
                    + " = " + (path.CacheSource ?? "new SKPathBuilder()") + ";");

                foreach (var build in path.BuildLines)
                    lines.Add(indent + path.BuilderName + "." + build);

                // A second shape under the same name would leak the first SKPath.
                if (path.Declared)
                    lines.Add(indent + name + ".Dispose();");

                lines.Add(indent + (path.Declared ? String.Empty : "SKPath ") + name + " = " + path.BuilderName + ".Detach();");

                // Detach() hands over ownership of a native object that nothing else frees. Leaving
                // it to the finalizer is measurably slower than disposing it here.
                if (!path.Declared)
                    this.currentContext.DisposablesInCurrentMethodAtNesting.Add(
                        new Tuple<int, string>(this.currentContext.ExtraNestingInMethod, name));
            }

            path.BuildLines.Clear();
            path.Declared = true;
            path.NeedsDetach = false;
        }

        /// <summary>
        /// Turns a cache slot that was emitted as a lazily created SKPathBuilder into a plain SKPath
        /// slot, now that the method body has shown the geometry to be the same on every call.
        /// </summary>
        private void RewritePathCacheDeclaration(string cacheSource)
        {
            if (!this.pathCacheLineIndex.TryGetValue(cacheSource, out var index))
                return;

            var name = cacheSource.Substring(cacheSource.IndexOf('.') + 1);

            this.output[index] =
                $"        private static SKPath {name}_store; public static SKPath {name} {{ get {{ return {name}_store; }} set {{ {name}_store = value; }} }}";

            this.pathCacheLineIndex.Remove(cacheSource);
        }

        /// <summary>
        /// Routes a typeface/size assignment at the SKFont half of a PaintCode TextPaint, declaring
        /// the font local the first time it is needed.
        /// </summary>
        private void AddTextFontLine(string translatedLine, string originalTrimmedLine)
        {
            var receiver = originalTrimmedLine.Split('.')[0];

            TextPaintVar textPaint;
            if (!this.currentContext.TextPaintsInMethod.TryGetValue(receiver, out textPaint))
            {
                this.currentContext.CurrentMethodLines.Add(translatedLine);
                return;
            }

            var trimmed = translatedLine.TrimStart();
            var indent = translatedLine.Substring(0, translatedLine.Length - trimmed.Length);

            if (!textPaint.FontDeclared)
            {
                this.currentContext.CurrentMethodLines.Add(String.Empty);
                this.currentContext.CurrentMethodLines.Add(indent + "var " + textPaint.FontName + " = " + textPaint.FontSource + ";");
                textPaint.FontDeclared = true;
            }

            this.currentContext.CurrentMethodLines.Add(indent + trimmed.ReplaceFirst(receiver, textPaint.FontName));
        }

        /// <summary>textTextPaint -> textTextFont, matching the cached pair.</summary>
        private static string FontNameFor(string textPaintName)
        {
            return textPaintName.EndsWith("Paint")
                ? textPaintName.Substring(0, textPaintName.Length - "Paint".Length) + "Font"
                : textPaintName + "Font";
        }

        private static string MemberName(string afterDot)
        {
            int i = 0;
            while (i < afterDot.Length && (Char.IsLetterOrDigit(afterDot[i]) || afterDot[i] == '_'))
                i++;

            return afterDot.Substring(0, i);
        }

        /// <summary>Whole-word match, so bezierPath does not match inside bezierPathBounds.</summary>
        private static bool ContainsWord(string line, string word)
        {
            int i = 0;

            while ((i = line.IndexOf(word, i, StringComparison.Ordinal)) >= 0)
            {
                var before = i == 0 ? ' ' : line[i - 1];
                var afterIndex = i + word.Length;
                var after = afterIndex >= line.Length ? ' ' : line[afterIndex];

                if (!Char.IsLetterOrDigit(before) && before != '_' && !Char.IsLetterOrDigit(after) && after != '_')
                    return true;

                i += word.Length;
            }

            return false;
        }

        private static string NextLine(string[] lines, int i)
        {
            if (lines.Length > i + 1)
                return lines[i + 1];

            return null;
        }

        private string ReplaceGetters(string line)
        {
            return line.Replace(".width()", ".Width");
        }

        private void ParseLineInEnum(string trimmedLine, string line)
        {
            if (!this.currentContext.OmitCurrentEnum)
            {
                this.output.Add(line);
            }

            if (trimmedLine == "}")
            {
                this.currentContext.FilePart = FilePart.Class;
                return;
            }
        }

        private void ParseLineInCacheClass(string trimmedLine, string line)
        {
            if (trimmedLine == "}")
            {
                this.currentContext.FilePart = FilePart.Class;
                this.output.Add(line);
                return;
            }

            var words = trimmedLine.Split(new char[] { ' ', '\t' });

            var type = words[2];
            var name = words[3];

            switch (type)
            {
                case "Paint":
                    this.output.Add($"        private static SKPaint {name}_store; public static SKPaint {name} {{ get {{ if ({name}_store == null) {name}_store = new SKPaint(); return {name}_store; }} }}");
                    break;

                case "RectF":
                    if (words[3] == "originalFrame")
                        this.output.Add(line.Replace("private static RectF", "public static SKRect").Replace("new RectF", "new SKRect"));

                    // ignore others
                    break;

                case "Path":
                    // SkiaSharp 4 builds geometry with SKPathBuilder; SKPath itself is snapshot-only now.
                    // The cache class is emitted before the method that fills it, so this starts as a
                    // builder and is rewritten to a plain SKPath slot if the geometry turns out to be
                    // constant. See RewritePathCacheDeclaration.
                    this.pathCacheLineIndex[this.currentCacheClassName + "." + name] = this.output.Count;
                    this.output.Add($"        private static SKPathBuilder {name}_store; public static SKPathBuilder {name} {{ get {{ if ({name}_store == null) {name}_store = new SKPathBuilder(); return {name}_store; }} }}");
                    break;

                case "TextPaint":
                    // SkiaSharp 4 moved the text state (typeface, size, ...) off SKPaint and onto
                    // SKFont, so one PaintCode TextPaint becomes a cached pair.
                    this.output.Add($"        private static SKPaint {name}_store; public static SKPaint {name} {{ get {{ if ({name}_store == null) {name}_store = new SKPaint(); return {name}_store; }} }}");

                    var fontName = FontNameFor(name);
                    this.output.Add($"        private static SKFont {fontName}_store; public static SKFont {fontName} {{ get {{ if ({fontName}_store == null) {fontName}_store = new SKFont(); return {fontName}_store; }} }}");
                    break;

                case "PaintCodeStaticLayout":
                    this.output.Add($"        private static PaintCodeStaticLayout {name}_store; public static PaintCodeStaticLayout {name} {{ get {{ if ({name}_store == null) {name}_store = new PaintCodeStaticLayout(); return {name}_store; }} }}");
                    break;

                case "PaintCodeGradient":
                    this.output.Add(line.Replace("private static", "public static"));
                    break;

                case "PaintCodeLinearGradient":
                    this.output.Add(line.Replace("private static", "public static"));
                    break;

                case "PaintCodeDashPathEffect":
                    this.output.Add(line.Replace("private static", "public static"));
                    break;

                case "PaintCodeRadialGradient":
                    this.output.Add(line.Replace("private static", "public static"));
                    break;

                case "float[]":
                    int size = 8;
                    if (name.Contains("GradientPoints"))
                        size = 4;

                    this.output.Add($"        private static float[] {name}_store; public static float[] {name} {{ get {{ if ({name}_store == null) {name}_store = new float[{size}]; return {name}_store; }} }}");
                    break;
                case "PaintCodeShadow":
                    //this.output.Add(line.Replace("private static", "public static"));
                    this.output.Add($"        private static PaintCodeShadow {name}_store; public static PaintCodeShadow {name} {{ get {{ if ({name}_store == null) {name}_store = new PaintCodeShadow(); return {name}_store; }} }}");
                    break;

                    //private static PaintCodeShadow shadow = new PaintCodeShadow();
                default:
                    Console.WriteLine("ERROR: Unknown data type in cache class:" + type);
                    break;
            }
        }

        private void ParseLineInMainClass(string trimmedLine, string line)
        {
            if (trimmedLine.StartsWith("public enum"))
            {
                this.currentContext.OmitCurrentEnum = true;

                if (!this.currentContext.OmitCurrentEnum)
                    this.output.Add(line);

                this.currentContext.FilePart = FilePart.Enum;
                return;
            }

            if (trimmedLine.EndsWith("}"))
            {
                this.output.Add(line);
                this.currentContext.FilePart = FilePart.AfterMainClass;
                return;
            }

            if (trimmedLine.StartsWith("private static class GlobalCache"))
            {
                line = line.Replace("private static", "internal static");

                this.output.Add(line);
                this.currentContext.FilePart = FilePart.GlobalCacheClass;
                //this.currentContext.CurrentNestedClassName 
            }

            if (trimmedLine.StartsWith("private static class Cache"))
            {
                line = line.Replace("private static", "internal static");

                this.currentCacheClassName = trimmedLine
                    .Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)[3]
                    .TrimEnd('{').Trim();

                this.output.Add(line);
                this.currentContext.FilePart = FilePart.CacheClass;
                //this.currentContext.CurrentNestedClassName
            }
            else if (trimmedLine.StartsWith("public static void") || trimmedLine.StartsWith("private static void"))
            {
                var firstBracketIndex = line.IndexOf('(');
                var parametersStr = line.Remove(0, firstBracketIndex + 1);
                var lastBracketIndex = parametersStr.LastIndexOf(')');
                parametersStr = parametersStr.Remove(lastBracketIndex, parametersStr.Length - lastBracketIndex);

                var parameters = parametersStr.Split(',');

                line = line.Remove(firstBracketIndex + 1, line.Length - firstBracketIndex - 1);

                string methodName = line.Split(new char[] { ' ', '(' }).Last(p => !String.IsNullOrEmpty(p.Trim()));

                this.currentContext.CurrentMethodNameAndModifiers = line;
                this.currentContext.CurrentMethodParameters.Clear();
                this.currentContext.CurrentMethodLines.Clear();
                this.currentContext.DisposablesInCurrentMethodAtNesting.Clear();
                this.currentContext.CurrentTempPaintsForSaveLayerAlpha = 0;
                this.currentContext.PathsInMethod.Clear();
                this.currentContext.TextPaintsInMethod.Clear();
                this.currentContext.ConstLocalsInMethod.Clear();

                for (int i = 0; i < parameters.Length; i++)
                {
                    var p = parameters[i];

                    var components = p.Trim().Split(' ');

                    if (dataTypesMap.ContainsKey(components[0]))
                        components[0] = dataTypesMap[components[0]];

                    if (components[0] == "int" && components[1].ToLower().Contains("color"))
                    {
                        components[0] = "SKColor";
                    }

                    this.currentContext.CurrentMethodParameters.Add(new Tuple<string, string>(components[0], components[1]));
                    //line += components[0] + " " + components[1];
                    //if (i < parameters.Length - 1)
                    //{
                    //    line += ", ";
                    //}
                }
                //line += ")";

                this.currentContext.FilePart = FilePart.Method;
                this.currentContext.ExtraNestingInMethod = 0;
                this.currentContext.CurrentMethodName = methodName;

                if (methodsInHelpers.Contains(methodName))
                {
                    this.currentContext.OmitCurrentMethod = true;
                }
                else
                {
                    this.currentContext.OmitCurrentMethod = false;

                    //this.currentContext.CurrentMethodLines.Add(line);
                    //this.output.Add(line);
                    this.currentContext.CurrentMethodLines.Add("    {");    
                    //this.output.Add("    {");
                }
            }
        }

        private void ParseLineInFileStart(string trimmedLine, string line)
        {
            if (line.StartsWith("package") || line.StartsWith("import"))
                return;

            if (trimmedLine.StartsWith("/*") || trimmedLine.StartsWith("*"))
            {
                output.Add(line);
                return;
            }

            var words = trimmedLine.Split(new char[] { ' ', '\t' });

            if (words.Length < 3)
                return;

            if (words[0] == "public" && words[1] == "class")
            {
                this.output.Add(line);
                this.currentContext.CurrentClassName = words[2];
                if (words[words.Length - 1] == "{")
                {
                    this.currentContext.FilePart = FilePart.Class;
                }
            }
        }
        
        private void AddOutRectsToMethods()
        {
            foreach (var methodKvp in methodsWithAddedOutRect)
            {
                var methodName = methodKvp.Key;

                // select lines containing method name and their indices
                var linesContainingMethodName = this.output
                    .Select((line, index) => new { line, index })
                    .Where(line => line.line.Contains($"{methodName}("))
                    .ToList();

                // problematic methods with out rect will be mentioned 3 times (signature1, call, signature2)
                if (linesContainingMethodName.Count != 3)
                    continue;

                var outRects = methodKvp.Value;

                // edit method signature to contain out rects
                var originalMethodSignature = linesContainingMethodName[0].line;
                var newMethodSignature = new StringBuilder(originalMethodSignature.Substring(0, originalMethodSignature.Length - 1));
                foreach (var outRect in outRects)
                {
                    newMethodSignature
                        .Append(", ")
                        .Append(outRect.Type)
                        .Append(" ")
                        .Append(outRect.Name);
                }

                newMethodSignature.Append(")");

                output[linesContainingMethodName[0].index] = newMethodSignature.ToString();

                // edit method call to contain out rects
                var originalMethodCall = linesContainingMethodName[1].line;
                var newMethodCall = new StringBuilder(originalMethodCall.Substring(0, originalMethodCall.Length - 2));
                foreach (var rect in outRects)
                {
                    newMethodCall
                        .Append(", out ")
                        .Append(rect.Name);
                }

                newMethodCall.Append(");");

                output[linesContainingMethodName[1].index] = newMethodCall.ToString();
            }
        }
    }
}
