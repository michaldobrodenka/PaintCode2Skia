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
            { "canvas.drawPath", "canvas.DrawPath" },
            {".setFlags(Paint.ANTI_ALIAS_FLAG);", ".IsAntialias = true;" },
            {".setStyle(Paint.Style.STROKE)", ".Style = SKPaintStyle.Stroke" },
            {".setStyle(Paint.Style.STROKE_AND_FILL)", ".Style = SKPaintStyle.StrokeAndFill" },
            {".setStyle(Paint.Style.FILL)", ".Style = SKPaintStyle.Fill" },
            {"Path.addRect", "Path.AddRect" },
            {"Path.addArc", "Path.AddArc" },
            {"Path.addRoundRect","Path.AddRoundedRect" },
            {"s.clipPath", "s.ClipPath" },
            {"Path.addOval", "Path.AddOval" },
            {"Path.close()", "Path.Close()" },
            {"new RectF", "new SKRect" },
            {".left", ".Left" },
            {".right", ".Right" },
            {".top", ".Top" },
            {".bottom", ".Bottom" },
            {"Path.Direction.CW" , "SKPathDirection.Clockwise" },
            {"Path.Direction.CCW" , "SKPathDirection.CounterClockwise" },
            {"Math.min", "Math.Min" },
            {"Math.max", "Math.Max" },
            {"Math.abs", "Math.Abs" },
            {"Math.ceil", "Math.Ceiling" },
            {"Math.round", "Math.Round" },
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
            { "Color.BLACK", "Helpers.ColorBlack" },
            {"Color.WHITE", "Helpers.ColorWhite" },
            {"Color.GRAY", "Helpers.ColorGray" },
            {".setXfermode(GlobalCache.blendModeMultiply)", ".BlendMode = SKBlendMode.Multiply" },
            {"Layout.Alignment.ALIGN_CENTER", "SKTextAlign.Center"},
            {"Layout.Alignment.ALIGN_NORMAL", "SKTextAlign.Left"},
            {"Layout.Alignment.ALIGN_OPPOSITE", "SKTextAlign.Right"},
            {"String.valueOf(", "Helpers.StringValueOf(" },
            {".postRotate(", " = SKMatrix.MakeRotationDegrees(" },
            {".transform(",".Transform("},
            {".invert(",".TryInvert(out " },
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
        }

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

            foreach (var line in javaLines)
            {
                var trimmedLine = line.Trim();

                if (String.IsNullOrEmpty(trimmedLine))
                {
                    if (!this.currentContext.LastLineWasNewLine)
                    {
                        this.output.Add(line);
                    }

                    this.currentContext.LastLineWasNewLine = true;
                    continue;
                }

                if (trimmedLine.StartsWith("//"))
                {
                    this.output.Add(line);
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
                        this.ParseLineInMethod(trimmedLine, line);
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

        private void ParseLineInMethod(string trimmedLine, string line)
        {
            if (trimmedLine.EndsWith("{"))
            {
                this.currentContext.ExtraNestingInMethod++;
            }
            else if (trimmedLine == "}")
            {
                if (this.currentContext.ExtraNestingInMethod > 0)
                    this.currentContext.ExtraNestingInMethod--;
                else
                    this.currentContext.FilePart = FilePart.Class;
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
                    this.output.Add(line);
                }
                else if (trimmedLine.StartsWith("//"))
                {
                    this.output.Add(line);
                }
                else if (trimmedLine.StartsWith("Paint "))
                {
                    this.output.Add(line.ReplaceFirst("Paint ", "SKPaint "));
                }
                else if (trimmedLine.StartsWith("Path "))
                {
                    this.output.Add(line.ReplaceFirst("Path ", "SKPath "));
                }
                else if (trimmedLine.StartsWith("int ") && (trimmedLine.Contains(" = Color.argb") || trimmedLine.Contains("Color")))
                {
                    this.output.Add(line.Replace("int ", "SKColor ").Replace(" = Color.argb", " = Helpers.ColorFromArgb"));
                }
                else if (trimmedLine.Contains(".resizingBehaviorApply("))
                {
                    this.output.Add("        var resizedFrame = " + trimmedLine.Replace(", resizedFrame);", ");").Replace(this.currentContext.CurrentClassName+ ".resizingBehaviorApply", "Helpers.ResizingBehaviorApply"));
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
                    this.output.Add(("        var " + trimmedLine.Split('.')[0] + " = new SKRect" + trimmedLine.Remove(0, trimmedLine.IndexOf('('))).ReplaceAll(gettersMap).ReplaceAll(simpleCommandsMap));
                    //this.output.Add("        var " + trimmedLine.Split('.')[0] + " = new SKRect(" + trimmedLine.Split('(')[1]);
                    this.currentContext.SkippingRectFfromCache = false;
                }
                else if (trimmedLine.Contains(".setTypeface(Typeface.createFromAsset(context.getAssets(), "))
                {
                    this.output.Add(line.Replace(".setTypeface(Typeface.createFromAsset(context.getAssets(), ", ".Typeface = TypefaceManager.GetTypeface(").Replace("));", ");"));
                }
                else if (trimmedLine.Contains(" new PaintCodeGradient") && trimmedLine.Contains("new int[]"))
                {
                    this.output.Add(line.Replace("new int[]", "new SKColor[]").ReplaceAll(simpleCommandsMap).ReplaceAll(gettersMap));
                }
                else if (trimmedLine.Contains("TextPaint "))
                {
                    this.output.Add(line.ReplaceFirst("TextPaint ", "var "));
                }
                else if (trimmedLine.Contains(".setShader("))
                {
                    var newLine = line.Replace(".setShader(", ".Shader = ");

                    if (!line.Contains("));"))
                        this.currentContext.NeedToReplaceTwoBrackets = true;

                    this.output.Add(newLine.Replace("));", ");"));
                }
                else if (trimmedLine.Contains(".setTextSize("))
                {
                    this.output.Add(line.ReplaceFirst(".setTextSize(", ".TextSize = ").Replace(")", ""));
                }
                else if (trimmedLine.Contains(".setStrokeWidth("))
                {
                    this.output.Add(line.ReplaceFirst(".setStrokeWidth(", ".StrokeWidth = ").Replace(")", ""));
                }
                else if (trimmedLine.Contains(".setStrokeMiter("))
                {
                    this.output.Add(line.ReplaceFirst(".setStrokeMiter(", ".StrokeMiter = ").Replace(")", ""));
                }
                else if (trimmedLine.Contains(".setColor("))
                {
                    this.output.Add(line.ReplaceFirst(".setColor(", ".Color = (SKColor)").Replace(");", ";").ReplaceAll(simpleCommandsMap).ReplaceAll(gettersMap));
                }
                else if (trimmedLine.Contains(".computeBounds("))
                {
                    var firstBracketIndex = line.IndexOf('(');
                    var parametersStr = line.Remove(0, firstBracketIndex + 1);
                    var bounds = parametersStr.Split(',')[0];
                    var path = trimmedLine.Split('.')[0];
                    var newLine = "        var " + bounds + " = " + path + ".ComputeTightBounds();";
                    this.output.Add(newLine);
                }
                else if (trimmedLine.Contains("Matrix"))
                {
                    if (trimmedLine.Contains("Stack<Matrix>") || trimmedLine.Contains(".push"))
                    {
                        this.output.Add("// " + line + " // skipping - we do not support Matrix yet");
                    }
                    else
                    {
                        this.output.Add(line.Replace("Matrix", "SKMatrix").Replace(" = ", "; //"));
                    }
                }
                else if (trimmedLine.Contains("Transformation.peek()"))
                {
                    this.output.Add("// " + line + " // skipping - we do not support Matrix yet");
                }
                else if (trimmedLine == "}")
                {
                    this.currentContext.NeedToReplaceTwoBrackets = false;
                    this.output.Add(line);
                }
                else
                {
                    this.output.Add(line.ReplaceAll(simpleCommandsMap).ReplaceAll(gettersMap));
                }
            }
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
            else if (trimmedLine.StartsWith("public static void"))
            {
                var firstBracketIndex = line.IndexOf('(');
                var parametersStr = line.Remove(0, firstBracketIndex + 1);
                var lastBracketIndex = parametersStr.LastIndexOf(')');
                parametersStr = parametersStr.Remove(lastBracketIndex, parametersStr.Length - lastBracketIndex);

                var parameters = parametersStr.Split(',');

                line = line.Remove(firstBracketIndex + 1, line.Length - firstBracketIndex - 1);

                string methodName = line.Split(new char[] { ' ', '(' }).Last(p => !String.IsNullOrEmpty(p.Trim()));

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

                    line += components[0] + " " + components[1];
                    if (i < parameters.Length - 1)
                    {
                        line += ", ";
                    }
                }
                line += ")";

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
                    this.output.Add(line);
                    this.output.Add("    {");
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
    }
}
