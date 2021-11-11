using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
            { "Canvas", "SKCanvas" },
            { "RectF", "SKRect" },
            { "boolean", "bool" },
            { "PointF", "SKPoint" },
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
            {".postRotate(", " = SKMatrix.MakeRotationDegrees(" },
            {".transform(",".Transform("},
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
                    this.currentContext.CurrentMethodLines.Add(line.ReplaceFirst("Path ", "SKPath "));
                }
                else if (trimmedLine.StartsWith("int ") && (trimmedLine.Contains(" = Color.argb") || trimmedLine.Contains("Color")))
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

                    this.currentContext.CurrentMethodLines.Add(("        var " + rectName + " = new SKRect" + trimmedLine.Remove(0, trimmedLine.IndexOf('('))).ReplaceAll(gettersMap).ReplaceAll(simpleCommandsMap));
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
                    this.currentContext.CurrentMethodLines.Add(line.Replace(".setTypeface(Typeface.createFromAsset(context.getAssets(), ", ".Typeface = TypefaceManager.GetTypeface(").Replace("));", ");"));
                }
                else if (trimmedLine.Contains(" new PaintCodeGradient") && trimmedLine.Contains("new int[]"))
                {
                    this.currentContext.CurrentMethodLines.Add(line.Replace("new int[]", "new SKColor[]").ReplaceAll(simpleCommandsMap).ReplaceAll(gettersMap));
                }
                else if (trimmedLine.Contains("TextPaint "))
                {
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
                else if (trimmedLine.Contains(".setTextSize("))
                {
                    this.currentContext.CurrentMethodLines.Add(line.ReplaceFirst(".setTextSize(", ".TextSize = ").Replace(")", ""));
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
                    this.currentContext.CurrentMethodLines.Add(newLine);
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
                else if (trimmedLine == "}")
                {
                    this.currentContext.NeedToReplaceTwoBrackets = false;
                    this.currentContext.CurrentMethodLines.Add(line);
                }
                else
                {
                    this.currentContext.CurrentMethodLines.Add(line.ReplaceAll(simpleCommandsMap).ReplaceAll(gettersMap));
                }
            }
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
                    this.output.Add($"        private static SKPath {name}_store; public static SKPath {name} {{ get {{ if ({name}_store == null) {name}_store = new SKPath(); return {name}_store; }} }}");
                    //this.output.Add(line.Replace("private static Path", "private static SKPath").Replace("new Path", "new SKPath"));
                    break;

                case "TextPaint":
                    this.output.Add($"        private static SKPaint {name}_store; public static SKPaint {name} {{ get {{ if ({name}_store == null) {name}_store = new SKPaint(); return {name}_store; }} }}");
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
                    Console.WriteLine("ERROR: Unknon data type in cache class:" + type);
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

                for (int i = 0; i < parameters.Length; i++)
                {
                    var p = parameters[i];

                    var components = p.Trim().Split(' ');

                    if (dataTypesMap.ContainsKey(components[0]))
                        components[0] = dataTypesMap[components[0]];

                    if (components[0] == "int" && components[1].Contains("Color"))
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
