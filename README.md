# PaintCode2Skia
Convert your PaintCode app Android Export java code to SkiaSharp C# code.

Simple transpiler (or "text replacer") which creates SkiaSharp C# code from PaintCode ( https://www.paintcodeapp.com/ ) Android Java Export.
Features were added until all icons I needed to convert were converted without errors. I'm using generated code on Xamarin.Android and Xamarin.iOS, in future in desktop .NET applications. Basically it is almost stupid string replacer. If it looks stupid but works it ain't stupid :)

Now we can enjoy PaintCode in WPF, Xamarin.Forms, WinForms, ASP.NET projects.

# SkiaSharp version

The generated code targets **SkiaSharp 4.x** (developed against 4.151.1) and uses no obsolete API.
Two things changed in SkiaSharp 4 and are reflected in the output:

- **Geometry is built with `SKPathBuilder`.** All the mutating methods on `SKPath`
  (`MoveTo`/`LineTo`/`CubicTo`/`AddRect`/`AddOval`/`Close`/...) are obsolete in SkiaSharp 4, while
  `SKCanvas.DrawPath` and `ClipPath` still take an `SKPath`. The generated code therefore builds
  through an `SKPathBuilder` and calls `Detach()` to get the `SKPath` it draws. Both types appear
  directly in the output - there is no wrapper class hiding the split.
- **Text state moved from `SKPaint` to `SKFont`.** `SKPaint.TextSize`/`Typeface`/`TextAlign` are obsolete
  and `SKCanvas.DrawText`/`DrawTextOnPath` without an `SKFont` are hard errors. A PaintCode `TextPaint`
  therefore becomes a pair of cached objects, `xTextPaint` (colour, antialiasing) and `xTextFont`
  (typeface, size), both of which are passed to `StaticLayout`.

## Cached path geometry

PaintCode applies resizing to the canvas rather than to the coordinates, so nearly every shape has the
same geometry on every call. The transpiler detects this: when every argument in a shape is a literal,
an SkiaSharp enum or a local that is itself constant, the shape is built once and kept in the cache as
a finished `SKPath`, and later draws reuse it. On one real export this applied to 3625 of 3667 paths
and removed all per-draw path allocation.

Shapes whose geometry does depend on the arguments are rebuilt through the cached `SKPathBuilder` as
before, and the `SKPath` that `Detach()` hands over is disposed at the end of the block it was made in.
Detection is deliberately conservative: anything the analysis does not recognise is treated as varying,
because a missed optimisation only costs a rebuild whereas a wrong one would freeze a shape that is
supposed to move.

`PaintCodeResources` stays on `netstandard2.0`, so it is consumable from .NET Framework through .NET 10.
For SkiaSharp 1.x/2.x output, use a commit from before the SkiaSharp 4 migration.


# What is working:
- basic features, lines, rects, colors, etc
- texts
- gradients
- Parameters!
-...

# What is missing:

- Some more complex Matrix transformations (basic rotation works)
- Layer opacity (could be simulated with color with Alfa)
- Probably many more features

# Why?
PaintCode is nice tool, but I'm missing export to Xamarin.Android and Windows desktop. Xamarin.Android can use StyleKitSharper tool (https://github.com/danielkatz/StyleKitSharper) but it creates a lot of static Java objects. That means thousands long living objects for hundreds of icons which are overloading GC Bridge.

# Sample clock from PaintCode tutorial:
![alt text](https://i.imgur.com/tso17vU.gif)


# Solution Structure:
- PaintCode2Skia - transpiler
- Sample
	- StyleKitName.java - sample export from PaintCode tutorial
	- StyleKitName.cs - transpiled sample
	- PaintCodeResources - .NET Standard 2.0 library for result icon drawing which can be used on every platform supporting .NET standard and SkiaSharp
		- Fonts - all used fonts, Build Action set to EmbeddedResource
		- PaintCodeClasses.cs - Helper classes needed for transpiled code to run
		- StyleKitName.cs - link to transpiled Sample
	- PaintCodeResources.Sample.WinForms - example of Paintcode icon used in WinForms app (net10.0-windows)
