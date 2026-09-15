using System.Collections.Generic;
using System;
using System.IO;
using System.Windows.Media;
using CodePacker.Models;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;

namespace CodePacker.Services;

public class PdfExportService
{
    public void ExportToPdf(string outputPath, string title, string packedContent, List<FileItem> images, int fontSizePt)
    {
        using var doc = new PdfDocument();
        doc.Info.Title = title;
        doc.Info.Author = "CodePacker AI Export";

        var font = new XFont("Courier New", fontSizePt, XFontStyle.Regular);
        var headerFont = new XFont("Courier New", fontSizePt + 3, XFontStyle.Bold);
        var subHeaderFont = new XFont("Courier New", 7, XFontStyle.Regular);

        double margin = 20;
        double lineSpacing = fontSizePt * 1.25;

        var page = doc.AddPage();
        var gfx = XGraphics.FromPdfPage(page);
        double currentY = margin;
        double maxY = page.Height - margin;
        double contentWidth = page.Width - (margin * 2);

        // Header
        gfx.DrawString($"# {title} | {DateTime.Now:yyyy-MM-dd HH:mm:ss}", headerFont, XBrushes.DarkSlateBlue, new XPoint(margin, currentY));
        currentY += 15;
        gfx.DrawLine(new XPen(XColors.LightGray, 0.5), margin, currentY, page.Width - margin, currentY);
        currentY += 10;

        var lines = packedContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var imageMap = images.ToDictionary(i => i.RelativePath, i => i);

        foreach (var line in lines)
        {
            if (line.StartsWith("[IMG:") && line.EndsWith("]"))
            {
                var imgKey = line[5..^1];
                if (imageMap.TryGetValue(imgKey, out var imgItem) && imgItem.ImageData != null)
                {
                    try
                    {
                        using var ms = new MemoryStream(imgItem.ImageData);
                        var xImage = XImage.FromStream(() => new MemoryStream(imgItem.ImageData));

                        double imgW = Math.Min(contentWidth, xImage.PixelWidth * 0.5);
                        double imgH = (imgW / xImage.PixelWidth) * xImage.PixelHeight;

                        if (currentY + imgH + 20 > maxY)
                        {
                            page = doc.AddPage();
                            gfx = XGraphics.FromPdfPage(page);
                            currentY = margin;
                        }

                        gfx.DrawString($"📷 {imgKey}", subHeaderFont, XBrushes.Gray, new XPoint(margin, currentY));
                        currentY += 10;
                        gfx.DrawImage(xImage, margin, currentY, imgW, imgH);
                        currentY += imgH + 10;
                        continue;
                    }
                    catch { }
                }
            }

            if (currentY + lineSpacing > maxY)
            {
                page = doc.AddPage();
                gfx = XGraphics.FromPdfPage(page);
                currentY = margin;
            }

            if (line.StartsWith(">>> "))
            {
                gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(240, 245, 250)), margin, currentY - 8, contentWidth, lineSpacing + 2);
                gfx.DrawString(line, headerFont, XBrushes.Navy, new XPoint(margin + 2, currentY));
            }
            else
            {
                gfx.DrawString(line, font, XBrushes.Black, new XPoint(margin, currentY));
            }

            currentY += lineSpacing;
        }

        doc.Save(outputPath);
    }
}