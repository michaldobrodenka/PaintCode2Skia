# PaintCode2Skia
Convert your PaintCode app Android Export java code to SkiaSharp C# code.

Simple transpiler (or "text replacer") which creates SkiaSharp C# code from PaintCode ( https://www.paintcodeapp.com/ ) Android Java Export.
Features were added until all icons I needed to convert were converted without errors. I'm using generated code on Xamarin.Android and Xamarin.iOS, in future in desktop .NET applications. Basically it is almost stupid string replacer. If it looks stupid but works it ain't stupid.

What is working:
- basic features, lines, rects, colors, etc
- texts
- gradients
- Parameters!
-...

What is missing:

- Some more complex Matrix transformations (basic rotation works)
- Layer opacity (could be simulated with color with Alfa)
- Probably many more features

Why?

PaintCode is nice tool, but I'm missing export to Xamarin.Android and Windows desktop. Xamarin.Android can use StyleKitSharper tool (https://github.com/danielkatz/StyleKitSharper) but it creates a lot of static Java objects. That means thousands long living objects for hundreds of icons which are overloading GC Bridge.

Solution Structure:

- PaintCode2Skia - transpiler
- Sample
	- StyleKitName.java - sample export from PaintCode tutorial
	- StyleKitName.cs - transpiled sample
	- PaintCodeResources - .NET Standard 2.0 library for result icon drawing which can be used on every platform supporting .NET standard and SkiaSharp
		- Fonts - all used fonts, Build Action set to EmbeddedResource
		- PaintCodeClasses.cs - Helper classes needed for transpiled code to run
		- StyleKitName.cs - link to transpiled Sample
	- PaintCodeResources.Sample.WinForms - example of Paintcode icon used in WinForms app
