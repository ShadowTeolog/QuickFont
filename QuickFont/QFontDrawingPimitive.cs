using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using OpenTK;

namespace QuickFont
{
    public class QFontDrawingPimitive
    {
        public readonly QFont Font;
        public readonly QFontRenderOptions Options;

#if DEBUG // Keep copy of string for debug purposes, only
        private string _DisplayText_dbg = "<processedtext>";
#endif
        private Vector3 PrintOffset;

        public QFontDrawingPimitive(QFont font, QFontRenderOptions options)
        {
            Font = font;
            Options = options;
        }

        public QFontDrawingPimitive(QFont font)
        {
            Font = font;
            Options = new QFontRenderOptions();
        }

        public SizeF LastSize { get; private set; }
        internal List<QVertex> CurrentVertexRepr { get; } = new List<QVertex>();
        internal List<QVertex> ShadowVertexRepr { get; } = new List<QVertex>();

        private float LineSpacing()
        {
            return (float) Math.Ceiling(Font.FontData.maxGlyphHeight * Options.LineSpacing);
        }

        private bool IsMonospacingActive()
        {
            return Font.FontData.IsMonospacingActive(Options);
        }

        private float MonoSpaceWidth()
        {
            return Font.FontData.GetMonoSpaceWidth(Options);
        }

        private void RenderDropShadow(float x, float y, char c, QFontGlyph nonShadowGlyph, QFontData shadowFont,
            ref Rectangle clippingRectangle)
        {
            //note can cast drop shadow offset to int, but then you can't move the shadow smoothly...
            if (shadowFont != null && Options.DropShadowActive)
            {
                var xOffset = Font.FontData.meanGlyphWidth * Options.DropShadowOffset.X +
                              nonShadowGlyph.rect.Width * 0.5f;
                var yOffset = Font.FontData.meanGlyphWidth * Options.DropShadowOffset.Y +
                              nonShadowGlyph.rect.Height * 0.5f + nonShadowGlyph.yOffset;
                RenderGlyph(x + xOffset, y + yOffset, c, shadowFont, ShadowVertexRepr, ref clippingRectangle);
            }
        }

        private bool ScissorsTest(ref float x, ref float y, ref float width, ref float height, ref float u1,
            ref float v1, ref float u2, ref float v2, Rectangle clipRectangle)
        {
            if (y > clipRectangle.Y + clipRectangle.Height)
            {
                var oldHeight = height;
                var delta = y - (clipRectangle.Y + clipRectangle.Height);
                y = clipRectangle.Y + clipRectangle.Height;
                height -= delta;

                if (height <= 0) return true;

                var dv = delta / oldHeight;

                v1 += dv * (v2 - v1);
            }

            if (y - height < clipRectangle.Y)
            {
                var oldHeight = height;
                var delta = y - height - clipRectangle.Y;

                height -= delta;

                if (height <= 0) return true;

                var dv = delta / oldHeight;

                v2 -= dv * (v2 - v1);
            }

            if (x < clipRectangle.X)
            {
                var oldWidth = width;
                var delta = clipRectangle.X - x;
                x = clipRectangle.X;
                width -= delta;

                if (width <= 0) return true;

                var du = delta / oldWidth;

                u1 += du * (u2 - u1);
            }

            if (x + width > clipRectangle.X + clipRectangle.Width)
            {
                var oldWidth = width;
                var delta = x + width - (clipRectangle.X + clipRectangle.Width);

                width -= delta;

                if (width <= 0) return true;

                var du = delta / oldWidth;

                u2 -= du * (u2 - u1);
            }

            return false;
        }

        /// <summary>
        ///     Renders the glyph at the position given.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="c">The character to print.</param>
        /// <param name="font">font used for render</param>
        /// <param name="store">target vertex buffer</param>
        internal void RenderGlyph(float x, float y, char c, QFontData fontdata, List<QVertex> store, ref Rectangle clippingRectangle)
        {
            var glyph = fontdata.CharSetMapping[c];

            //note: it's not immediately obvious, but this combined with the paramteters to 
            //RenderGlyph for the shadow mean that we render the shadow centrally (despite it being a different size)
            //under the glyph
            if (fontdata.isDropShadow)
            {
                x -= (int) (glyph.rect.Width * 0.5f);
                y -= (int) (glyph.rect.Height * 0.5f + glyph.yOffset);
            }
            else
            {
                RenderDropShadow(x, y, c, glyph, fontdata.dropShadowFont?.FontData, ref clippingRectangle);
            }

            y = -y;

            var sheet = fontdata.Pages[glyph.page];

            var tx1 = (float) glyph.rect.X / sheet.Width;
            var ty1 = (float) glyph.rect.Y / sheet.Height;
            var tx2 = (float) (glyph.rect.X + glyph.rect.Width) / sheet.Width;
            var ty2 = (float) (glyph.rect.Y + glyph.rect.Height) / sheet.Height;

            var vx = x + PrintOffset.X;
            var vy = y - glyph.yOffset + PrintOffset.Y;
            float vwidth = glyph.rect.Width;
            float vheight = glyph.rect.Height;

            if (clippingRectangle != default(Rectangle) && ScissorsTest(ref vx, ref vy, ref vwidth, ref vheight,
                    ref tx1, ref ty1, ref tx2, ref ty2, clippingRectangle)) return;

            var tv1 = new Vector2(tx1, ty1);
            var tv2 = new Vector2(tx1, ty2);
            var tv3 = new Vector2(tx2, ty2);
            var tv4 = new Vector2(tx2, ty1);

            var v1 = new Vector3(vx, vy, PrintOffset.Z);
            var v2 = new Vector3(vx, vy - vheight, PrintOffset.Z);
            var v3 = new Vector3(vx + vwidth, vy - vheight, PrintOffset.Z);
            var v4 = new Vector3(vx + vwidth, vy, PrintOffset.Z);

            var color = fontdata.isDropShadow ? Options.DropShadowColour : Options.Colour;

            var colour = Helper.ToVector4(color);

            store.Add(new QVertex {Position = v1, TextureCoord = tv1, VertexColor = colour});
            store.Add(new QVertex {Position = v2, TextureCoord = tv2, VertexColor = colour});
            store.Add(new QVertex {Position = v3, TextureCoord = tv3, VertexColor = colour});

            store.Add(new QVertex {Position = v1, TextureCoord = tv1, VertexColor = colour});
            store.Add(new QVertex {Position = v3, TextureCoord = tv3, VertexColor = colour});
            store.Add(new QVertex {Position = v4, TextureCoord = tv4, VertexColor = colour});
        }

        private float MeasureNextlineLength(string text)
        {
            var isMonospacingActive = IsMonospacingActive();
            float xOffset = 0;
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '\r' || c == '\n') break;
                if (isMonospacingActive)
                {
                    xOffset += MonoSpaceWidth();
                }
                else
                {
                    QFontGlyph glyph;
                    if (c == ' ')
                        xOffset += (float) Math.Ceiling(Font.FontData.meanGlyphWidth * Options.WordSpacing);
                    else if (Font.FontData.CharSetMapping.TryGetValue(c, out glyph)) //normal character
                        xOffset +=
                            (float)
                            Math.Ceiling(glyph.rect.Width + Font.FontData.meanGlyphWidth * Options.CharacterSpacing +
                                         Font.FontData.GetKerningPairCorrection(i, text, null));
                }
            }

            return xOffset;
        }

        private Vector2 TransformPositionToViewport(Vector2 input)
        {
            var v2 = Options.TransformToViewport;
            if (v2 == null) return input;
            var v1 = ViewportHelper.CurrentViewport;

            float X, Y;

            Debug.Assert(v1 != null, "v1 != null");
            X = (input.X - v2.Value.X) * (v1.Value.Width / v2.Value.Width);
            Y = (input.Y - v2.Value.Y) * (v1.Value.Height / v2.Value.Height);

            return new Vector2(X, Y);
        }

        private static float TransformWidthToViewport(float input, QFontRenderOptions options)
        {
            var v2 = options.TransformToViewport;
            if (v2 == null) return input;
            var v1 = ViewportHelper.CurrentViewport;

            Debug.Assert(v1 != null, "v1 != null");
            return input * (v1.Value.Width / v2.Value.Width);
        }

        private SizeF TransformMeasureFromViewport(SizeF input)
        {
            var v2 = Options.TransformToViewport;
            if (v2 == null) return input;
            var v1 = ViewportHelper.CurrentViewport;

            float X, Y;

            Debug.Assert(v1 != null, "v1 != null");
            X = input.Width * (v2.Value.Width / v1.Value.Width);
            Y = input.Height * (v2.Value.Height / v1.Value.Height);

            return new SizeF(X, Y);
        }

        private Vector2 LockToPixel(Vector2 input)
        {
            if (Options.LockToPixel)
            {
                var r = Options.LockToPixelRatio;
                return new Vector2((1 - r) * input.X + r * (int) Math.Round(input.X),
                    (1 - r) * input.Y + r * (int) Math.Round(input.Y));
            }

            return input;
        }

        private Vector3 TransformToViewport(Vector3 input)
        {
            return new Vector3(LockToPixel(TransformPositionToViewport(input.Xy))) {Z = input.Z};
        }

        private void PrepareVertexCapacityFromLetterCount(int LetterCount)
        {
            CurrentVertexRepr.Capacity += LetterCount * 4;
        }
        public SizeF Print(string text, Vector3 position, QFontAlignment alignment,
            Rectangle clippingRectangle = default(Rectangle))
        {
            PrepareVertexCapacityFromLetterCount(text.Length);
            PrintOffset = TransformToViewport(position);
            return PrintOrMeasure(text, alignment, false, clippingRectangle);
        }

        public SizeF Print(string text, Vector3 position, QFontAlignment alignment, Color color,
            Rectangle clippingRectangle = default(Rectangle))
        {
            PrepareVertexCapacityFromLetterCount(text.Length);
            Options.Colour = color;
            PrintOffset = TransformToViewport(position);
            return PrintOrMeasure(text, alignment, false, clippingRectangle);
        }

        public SizeF Print(string text, Vector3 position, SizeF maxSize, QFontAlignment alignment,
            Rectangle clippingRectangle = default(Rectangle))
        {
            var processedText = ProcessText(Font, Options, text, maxSize, alignment);
            return Print(processedText, TransformToViewport(position), clippingRectangle);
        }

        public SizeF Print(ProcessedText processedText, Vector3 position,
            Rectangle clippingRectangle = default(Rectangle))
        {
            PrepareVertexCapacityFromLetterCount(processedText.EstimatedLength());
            PrintOffset = TransformToViewport(position);
            return PrintOrMeasure(processedText, false, clippingRectangle);
        }

        public SizeF Print(ProcessedText processedText, Vector3 position, Color colour,
            Rectangle clippingRectangle = default(Rectangle))
        {
            PrepareVertexCapacityFromLetterCount(processedText.EstimatedLength());
            Options.Colour = colour;
            PrintOffset = TransformToViewport(position);
            return PrintOrMeasure(processedText, false, clippingRectangle);
        }

        public SizeF Measure(string text, QFontAlignment alignment = QFontAlignment.Left)
        {
            return TransformMeasureFromViewport(PrintOrMeasure(text, alignment, true));
        }

        public SizeF Measure(string text, float maxWidth, QFontAlignment alignment)
        {
            return Measure(text, new SizeF(maxWidth, -1), alignment);
        }

        /// <summary>
        ///     Measures the actual width and height of the block of text.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="bounds"></param>
        /// <param name="alignment"></param>
        /// <returns></returns>
        public SizeF Measure(string text, SizeF maxSize, QFontAlignment alignment)
        {
            var processedText = ProcessText(Font, Options, text, maxSize, alignment);
            return Measure(processedText);
        }

        /// <summary>
        ///     Measures the actual width and height of the block of text
        /// </summary>
        /// <param name="processedText"></param>
        /// <returns></returns>
        public SizeF Measure(ProcessedText processedText)
        {
            return TransformMeasureFromViewport(PrintOrMeasure(processedText, true));
        }

        private SizeF PrintOrMeasure(string text, QFontAlignment alignment, bool measureOnly,
            Rectangle clippingRectangle = default(Rectangle))
        {
            var maxWidth = 0f;
            var xOffset = 0f;
            var yOffset = 0f;

            var maxXpos = float.MinValue;
            var minXPos = float.MaxValue;

            text = text.Replace("\r\n", "\r");
#if DEBUG
            _DisplayText_dbg = text;
#endif
            if (alignment == QFontAlignment.Right)
                xOffset -= MeasureNextlineLength(text);
            else if (alignment == QFontAlignment.Centre)
                xOffset -= (int) (0.5f * MeasureNextlineLength(text));
            var lineSpacing = LineSpacing();
            var isMonospacingActive = IsMonospacingActive();
            var fontdata = Font?.FontData;
            if(fontdata!=null)
                
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                //newline
                if (c == '\r' || c == '\n')
                {
                    yOffset += lineSpacing;
                    xOffset = 0f;

                    if (alignment == QFontAlignment.Right)
                        xOffset -= MeasureNextlineLength(text.Substring(i + 1));
                    else if (alignment == QFontAlignment.Centre)
                        xOffset -= (int) (0.5f * MeasureNextlineLength(text.Substring(i + 1)));
                }
                else
                {
                    minXPos = Math.Min(xOffset, minXPos);

                    if (c == ' ')
                    {
                        xOffset += isMonospacingActive
                            ? MonoSpaceWidth()
                                : (float) Math.Ceiling(fontdata.meanGlyphWidth * Options.WordSpacing);
                    }
                    else
                    {
                        QFontGlyph glyph;
                        //normal character
                            if (fontdata.CharSetMapping.TryGetValue(c, out glyph))
                        {
                            if (!measureOnly)
                                    RenderGlyph(xOffset, yOffset, c, fontdata, CurrentVertexRepr, ref clippingRectangle);
                            if (isMonospacingActive)
                                xOffset += MonoSpaceWidth();
                            else
                                xOffset += (float) Math.Ceiling(
                                    glyph.rect.Width + Font.FontData.meanGlyphWidth * Options.CharacterSpacing +
                                        fontdata.GetKerningPairCorrection(i, text, null));
                        }
                    }

                    maxXpos = Math.Max(xOffset, maxXpos);
                }
            }

            if (minXPos != float.MaxValue)
                maxWidth = maxXpos - minXPos;

            LastSize = new SizeF(maxWidth, yOffset + lineSpacing);
            return LastSize;
        }

        private SizeF PrintOrMeasure(ProcessedText processedText, bool measureOnly,
            Rectangle clippingRectangle = default(Rectangle))
        {
            // init values we'll return
            var maxMeasuredWidth = 0f;

            var xPos = 0f;
            var yPos = 0f;

            var xOffset = xPos;
            var yOffset = yPos;

            //make sure fontdata font's options are synced with the actual options
            ////if (_font.FontData.dropShadowFont != null && _font.FontData.dropShadowFont.Options != this.Options)
            ////{
            ////    _font.FontData.dropShadowFont.Options = this.Options;
            ////}

            var maxWidth = processedText.maxSize.Width;
            var alignment = processedText.alignment;


            //TODO - use these instead of translate when rendering by position (at some point)

            var nodeList = processedText.textNodeList;
            for (var node = nodeList.Head; node != null; node = node.Next)
                node.LengthTweak = 0f; //reset tweaks


            if (alignment == QFontAlignment.Right)
                xOffset -= (float) Math.Ceiling(TextNodeLineLength(nodeList.Head, maxWidth) - maxWidth);
            else if (alignment == QFontAlignment.Centre)
                xOffset -= (float) Math.Ceiling(0.5f * TextNodeLineLength(nodeList.Head, maxWidth));
            else if (alignment == QFontAlignment.Justify)
                JustifyLine(nodeList.Head, maxWidth);


            var atLeastOneNodeCosumedOnLine = false;
            var length = 0f;
            var lineSpacing = LineSpacing();
            for (var node = nodeList.Head; node != null; node = node.Next)
            {
                var newLine = false;

                if (node.Type == TextNodeType.LineBreak)
                {
                    newLine = true;
                }
                else
                {
                    if (Options.WordWrap && SkipTrailingSpace(node, length, maxWidth) && atLeastOneNodeCosumedOnLine)
                    {
                        newLine = true;
                    }
                    else if (length + node.ModifiedLength <= maxWidth || !atLeastOneNodeCosumedOnLine)
                    {
                        atLeastOneNodeCosumedOnLine = true;

                        if (!measureOnly)
                            RenderWord(xOffset + length, yOffset, node, ref clippingRectangle);
                        length += node.ModifiedLength;

                        maxMeasuredWidth = Math.Max(length, maxMeasuredWidth);
                    }
                    else if (Options.WordWrap)
                    {
                        newLine = true;
                        if (node.Previous != null)
                            node = node.Previous;
                    }
                    else
                    {
                        continue; // continue so we still read line breaks even if reached max width
                    }
                }

                if (newLine)
                {
                    if (processedText.maxSize.Height > 0 &&
                        yOffset + lineSpacing - yPos >= processedText.maxSize.Height)
                        break;

                    yOffset += lineSpacing;
                    xOffset = xPos;
                    length = 0f;
                    atLeastOneNodeCosumedOnLine = false;

                    if (node.Next != null)
                    {
                        if (alignment == QFontAlignment.Right)
                            xOffset -= (float) Math.Ceiling(TextNodeLineLength(node.Next, maxWidth) - maxWidth);
                        else if (alignment == QFontAlignment.Centre)
                            xOffset -= (float) Math.Ceiling(0.5f * TextNodeLineLength(node.Next, maxWidth));
                        else if (alignment == QFontAlignment.Justify)
                            JustifyLine(node.Next, maxWidth);
                    }
                }
            }

            LastSize = new SizeF(maxMeasuredWidth, yOffset + lineSpacing - yPos);
            return LastSize;
        }

        private void RenderWord(float x, float y, TextNode node, ref Rectangle clippingRectangle)
        {
            if (node.Type != TextNodeType.Word)
                return;

            var charGaps = node.Text.Length - 1;
            var isCrumbleWord = CrumbledWord(node);
            if (isCrumbleWord)
                charGaps++;

            var pixelsPerGap = 0;
            var leftOverPixels = 0;

            if (charGaps != 0)
            {
                pixelsPerGap = (int) node.LengthTweak / charGaps;
                leftOverPixels = (int) node.LengthTweak - pixelsPerGap * charGaps;
            }

            var isMonospacingActive = IsMonospacingActive();
            var fontdata = Font?.FontData;
            if(fontdata!=null)
            for (var i = 0; i < node.Text.Length; i++)
            {
                var c = node.Text[i];
                QFontGlyph glyph;
                if (fontdata.CharSetMapping.TryGetValue(c, out glyph))
                {
                    RenderGlyph(x, y, c, fontdata, CurrentVertexRepr, ref clippingRectangle);
                    if (isMonospacingActive)
                        x += MonoSpaceWidth();
                    else
                        x +=
                            (int)
                            Math.Ceiling(glyph.rect.Width +fontdata.meanGlyphWidth * Options.CharacterSpacing +
                                         fontdata.GetKerningPairCorrection(i, node.Text, node));

                    x += pixelsPerGap;
                    if (leftOverPixels > 0)
                    {
                        x += 1.0f;
                        leftOverPixels--;
                    }
                    else if (leftOverPixels < 0)
                    {
                        x -= 1.0f;
                        leftOverPixels++;
                    }
                }
            }
        }

        /// <summary>
        ///     Computes the length of the next line, and whether the line is valid for
        ///     justification.
        /// </summary>
        /// <param name="node"></param>
        /// <param name="maxLength"></param>
        /// <param name="justifable"></param>
        /// <returns></returns>
        private float TextNodeLineLength(TextNode node, float maxLength)
        {
            if (node == null)
                return 0;

            var atLeastOneNodeCosumedOnLine = false;
            float length = 0;
            for (; node != null; node = node.Next)
            {
                if (node.Type == TextNodeType.LineBreak)
                    break;

                if (SkipTrailingSpace(node, length, maxLength) && atLeastOneNodeCosumedOnLine)
                    break;

                if (length + node.Length <= maxLength || !atLeastOneNodeCosumedOnLine)
                {
                    atLeastOneNodeCosumedOnLine = true;
                    length += node.Length;
                }
                else
                {
                    break;
                }
            }

            return length;
        }

        private bool CrumbledWord(TextNode node)
        {
            return node.Type == TextNodeType.Word && node.Next != null && node.Next.Type == TextNodeType.Word;
        }

        /// <summary>
        ///     Computes the length of the next line, and whether the line is valid for
        ///     justification.
        /// </summary>
        private void JustifyLine(TextNode node, float targetLength)
        {
            var justifiable = false;

            if (node == null)
                return;

            var headNode = node; //keep track of the head node


            //start by finding the length of the block of text that we know will actually fit:

            var charGaps = 0;
            var spaceGaps = 0;

            var atLeastOneNodeCosumedOnLine = false;
            float length = 0;
            var expandEndNode = node; //the node at the end of the smaller list (before adding additional word)
            for (; node != null; node = node.Next)
            {
                if (node.Type == TextNodeType.LineBreak)
                    break;

                if (SkipTrailingSpace(node, length, targetLength) && atLeastOneNodeCosumedOnLine)
                {
                    justifiable = true;
                    break;
                }

                if (length + node.Length < targetLength || !atLeastOneNodeCosumedOnLine)
                {
                    expandEndNode = node;

                    if (node.Type == TextNodeType.Space)
                        spaceGaps++;

                    if (node.Type == TextNodeType.Word)
                    {
                        charGaps += node.Text.Length - 1;

                        //word was part of a crumbled word, so there's an extra char cap between the two words
                        if (CrumbledWord(node))
                            charGaps++;
                    }

                    atLeastOneNodeCosumedOnLine = true;
                    length += node.Length;
                }
                else
                {
                    justifiable = true;
                    break;
                }
            }

            //now we check how much additional length is added by adding an additional word to the line
            var extraLength = 0f;
            var extraSpaceGaps = 0;
            var extraCharGaps = 0;
            var contractPossible = false;
            TextNode contractEndNode = null;
            for (node = expandEndNode.Next; node != null; node = node.Next)
            {
                if (node.Type == TextNodeType.LineBreak)
                    break;

                if (node.Type == TextNodeType.Space)
                {
                    extraLength += node.Length;
                    extraSpaceGaps++;
                }
                else if (node.Type == TextNodeType.Word)
                {
                    contractEndNode = node;
                    contractPossible = true;
                    extraLength += node.Length;
                    extraCharGaps += node.Text.Length - 1;
                    break;
                }
            }

            if (justifiable)
            {
                //last part of this condition is to ensure that the full contraction is possible (it is all or nothing with contractions, since it looks really bad if we don't manage the full)
                var contract = contractPossible &&
                               (extraLength + length - targetLength) * Options.JustifyContractionPenalty <
                               targetLength - length &&
                               (targetLength - (length + extraLength + 1)) / targetLength > -Options.JustifyCapContract;

                if (!contract && length < targetLength || contract && length + extraLength > targetLength)
                    //calculate padding pixels per word and char
                {
                    if (contract)
                    {
                        length += extraLength + 1;
                        charGaps += extraCharGaps;
                        spaceGaps += extraSpaceGaps;
                    }


                    var totalPixels = (int) (targetLength - length);
                    //the total number of pixels that need to be added to line to justify it
                    var spacePixels = 0; //number of pixels to spread out amongst spaces
                    var charPixels = 0; //number of pixels to spread out amongst char gaps


                    if (contract)
                    {
                        if (totalPixels / targetLength < -Options.JustifyCapContract)
                            totalPixels = (int) (-Options.JustifyCapContract * targetLength);
                    }
                    else
                    {
                        if (totalPixels / targetLength > Options.JustifyCapExpand)
                            totalPixels = (int) (Options.JustifyCapExpand * targetLength);
                    }

                    //work out how to spread pixles between character gaps and word spaces
                    if (charGaps == 0)
                    {
                        spacePixels = totalPixels;
                    }
                    else if (spaceGaps == 0)
                    {
                        charPixels = totalPixels;
                    }
                    else
                    {
                        if (contract)
                            charPixels =
                                (int) (totalPixels * Options.JustifyCharacterWeightForContract * charGaps / spaceGaps);
                        else
                            charPixels = (int) (totalPixels * Options.JustifyCharacterWeightForExpand * charGaps /
                                                spaceGaps);


                        if (!contract && charPixels > totalPixels ||
                            contract && charPixels < totalPixels)
                            charPixels = totalPixels;

                        spacePixels = totalPixels - charPixels;
                    }


                    var pixelsPerChar = 0; //minimum number of pixels to add per char
                    var leftOverCharPixels = 0; //number of pixels remaining to only add for some chars

                    if (charGaps != 0)
                    {
                        pixelsPerChar = charPixels / charGaps;
                        leftOverCharPixels = charPixels - pixelsPerChar * charGaps;
                    }

                    var pixelsPerSpace = 0; //minimum number of pixels to add per space
                    var leftOverSpacePixels = 0; //number of pixels remaining to only add for some spaces

                    if (spaceGaps != 0)
                    {
                        pixelsPerSpace = spacePixels / spaceGaps;
                        leftOverSpacePixels = spacePixels - pixelsPerSpace * spaceGaps;
                    }

                    //now actually iterate over all nodes and set tweaked length
                    for (node = headNode; node != null; node = node.Next)
                    {
                        if (node.Type == TextNodeType.Space)
                        {
                            node.LengthTweak = pixelsPerSpace;
                            if (leftOverSpacePixels > 0)
                            {
                                node.LengthTweak += 1;
                                leftOverSpacePixels--;
                            }
                            else if (leftOverSpacePixels < 0)
                            {
                                node.LengthTweak -= 1;
                                leftOverSpacePixels++;
                            }
                        }
                        else if (node.Type == TextNodeType.Word)
                        {
                            var cGaps = node.Text.Length - 1;
                            if (CrumbledWord(node))
                                cGaps++;

                            node.LengthTweak = cGaps * pixelsPerChar;


                            if (leftOverCharPixels >= cGaps)
                            {
                                node.LengthTweak += cGaps;
                                leftOverCharPixels -= cGaps;
                            }
                            else if (leftOverCharPixels <= -cGaps)
                            {
                                node.LengthTweak -= cGaps;
                                leftOverCharPixels += cGaps;
                            }
                            else
                            {
                                node.LengthTweak += leftOverCharPixels;
                                leftOverCharPixels = 0;
                            }
                        }

                        if (!contract && node == expandEndNode || contract && node == contractEndNode)
                            break;
                    }
                }
            }
        }

        /// <summary>
        ///     Checks whether to skip trailing space on line because the next word does not
        ///     fit.
        ///     We only check one space - the assumption is that if there is more than one,
        ///     it is a deliberate attempt to insert spaces.
        /// </summary>
        /// <param name="node"></param>
        /// <param name="lengthSoFar"></param>
        /// <param name="boundWidth"></param>
        /// <returns></returns>
        private bool SkipTrailingSpace(TextNode node, float lengthSoFar, float boundWidth)
        {
            if (node.Type == TextNodeType.Space && node.Next != null && node.Next.Type == TextNodeType.Word &&
                node.ModifiedLength + node.Next.ModifiedLength + lengthSoFar > boundWidth)
                return true;

            return false;
        }

        /// <summary>
        ///     Creates node list object associated with the text.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="bounds"></param>
        /// <returns></returns>
        public static ProcessedText ProcessText(QFont font, QFontRenderOptions options, string text, SizeF maxSize,
            QFontAlignment alignment)
        {
            //TODO: bring justify and alignment calculations in here
            maxSize.Width = TransformWidthToViewport(maxSize.Width, options);

            var nodeList = new TextNodeList(text);
            nodeList.MeasureNodes(font.FontData, options);

            //we "crumble" words that are two long so that that can be split up
            var nodesToCrumble = new List<TextNode>();
            foreach (TextNode node in nodeList)
                if ((!options.WordWrap || node.Length >= maxSize.Width) && node.Type == TextNodeType.Word)
                    nodesToCrumble.Add(node);

            foreach (var node in nodesToCrumble)
                nodeList.Crumble(node, 1);

            //need to measure crumbled words
            nodeList.MeasureNodes(font.FontData, options);


            var processedText = new ProcessedText();
            processedText.textNodeList = nodeList;
            processedText.maxSize = maxSize;
            processedText.alignment = alignment;


            return processedText;
        }
    }
}