###ModernQuickFont ES 2.0
Fork of original version in reason to be more optimized in text preparations.
Currently a little crazy work with buffers inside, and this slow.
Currently done:
 -[x] add Capacity enlargement before adding vertextes to DrawingPrimitive list.
 -[x] remove dictionary doubled key search in most places
 -[x] stop calling some computed properties code inside for loops
 -[ ] make vertexes stored directly in global List but not in dozens small lists.
  

##Code
So how would the code look like, now?

In some OnLoad() method create your QFont and your QFontDrawing
```C#
_myFont = new QFont("Fonts/HappySans.ttf", 72, new QFontBuilderConfiguration(true));
_myFont2 = new QFont("basics.qfont", new QFontBuilderConfiguration(true));
_drawing = new QFontDrawing();
```

On Event (to create screen) call some print methods or create Drawing primitives by themselves.
Add them to the drawing.
```C#
_drawing.Clear();
_drawing.Print(_myFont, "text1", pos, FontAlignment.Left);

// draw with options
var textOpts = new QFontRenderOptions()
    {
	Colour = Color.FromArgb(new Color4(0.8f, 0.1f, 0.1f, 1.0f).ToArgb()),
	DropShadowActive = true
	};
SizeF size = _drawing.Print(_myFont, "text2", pos2, FontAlignment.Left, textOpts);
size = drawing.Print(_myFont2,text, new Vector3(bounds.X, Height - yOffset, 0), new SizeF(maxWidth, float.MaxValue), alignment);
// after all changes do update buffer data and extend it's size if needed.
_drawing.RefreshBuffers();

```

Then in your paint-loop do:
```C#
_drawing.ProjectionMatrix = proj;
_drawing.Draw();
SwapBuffers();
```

At the end of the program dispose your own resources:
```C#
protected virtual void Dispose(bool disposing)
{
	_drawing.Dispose();
	_myFont.Dispose();
	_myFont2.Dispose();
}
```


###Please note this API is not backwards compatible with all previous QuickFont releases hence the new Version 3
