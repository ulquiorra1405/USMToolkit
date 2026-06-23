using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Markdig;

namespace Toolkit.Services;

/// <summary>
/// Convierte texto Markdown a un FlowDocument de WPF.
/// Soporta: headings, párrafos, listas, imágenes, código, bold, italic, links.
/// </summary>
public class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public FlowDocument Render(string markdown, string basePath)
    {
        var doc = new FlowDocument
        {
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 14,
            TextAlignment = TextAlignment.Left,
            PagePadding = new Thickness(0),
            LineHeight = 1.4
        };

        if (string.IsNullOrEmpty(markdown))
        {
            doc.Blocks.Add(new Paragraph(new Run("Sin contenido."))
            {
                Foreground = Brushes.Gray,
                FontStyle = FontStyles.Italic
            });
            return doc;
        }

        var mdDoc = Markdown.Parse(markdown, Pipeline);
        var baseDir = Path.GetDirectoryName(basePath) ?? "";

        foreach (var block in mdDoc)
        {
            switch (block)
            {
                case Markdig.Syntax.HeadingBlock heading:
                    AddHeading(doc, heading);
                    break;
                case Markdig.Syntax.ParagraphBlock paragraph:
                    AddParagraph(doc, paragraph, baseDir);
                    break;
                case Markdig.Syntax.ListBlock listBlock:
                    AddList(doc, listBlock, baseDir);
                    break;
                case Markdig.Syntax.FencedCodeBlock fcb:
                    AddFencedCode(doc, fcb);
                    break;
                case Markdig.Syntax.CodeBlock scb:
                    AddSimpleCode(doc, scb);
                    break;
                case Markdig.Syntax.ThematicBreakBlock:
                    AddHorizontalRule(doc);
                    break;
                case Markdig.Syntax.QuoteBlock quoteBlock:
                    AddQuoteBlock(doc, quoteBlock, baseDir);
                    break;
            }
        }

        return doc;
    }

    private void AddHeading(FlowDocument doc, Markdig.Syntax.HeadingBlock heading)
    {
        var text = GetInlineText(heading);
        if (string.IsNullOrEmpty(text)) return;

        var run = new Run(text);
        var para = new Paragraph(run);

        switch (heading.Level)
        {
            case 1:
                para.FontSize = 26;
                para.FontWeight = FontWeights.Light;
                para.Margin = new Thickness(0, 0, 0, 16);
                break;
            case 2:
                para.FontSize = 20;
                para.FontWeight = FontWeights.SemiBold;
                para.Margin = new Thickness(0, 20, 0, 8);
                break;
            case 3:
                para.FontSize = 16;
                para.FontWeight = FontWeights.SemiBold;
                para.Margin = new Thickness(0, 16, 0, 6);
                break;
            default:
                para.FontSize = 14;
                para.FontWeight = FontWeights.SemiBold;
                para.Margin = new Thickness(0, 12, 0, 4);
                break;
        }

        para.Foreground = Foreground;
        doc.Blocks.Add(para);
    }

    private void AddParagraph(FlowDocument doc, Markdig.Syntax.ParagraphBlock paragraph, string baseDir)
    {
        var para = new Paragraph
        {
            Margin = new Thickness(0, 0, 0, 10),
            Foreground = Foreground
        };

        if (paragraph.Inline != null)
        {
            foreach (var inline in paragraph.Inline)
                ProcessInline(para, inline, baseDir);
        }

        doc.Blocks.Add(para);
    }

    private void AddList(FlowDocument doc, Markdig.Syntax.ListBlock listBlock, string baseDir)
    {
        var isOrdered = listBlock.IsOrdered;

        var list = new List();
        list.MarkerStyle = isOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc;
        list.Margin = new Thickness(20, 0, 0, 10);

        foreach (var item in listBlock)
        {
            if (item is Markdig.Syntax.ListItemBlock listItem)
            {
                var li = new ListItem();
                var para = new Paragraph
                {
                    Margin = new Thickness(0, 2, 0, 2),
                    Foreground = Foreground
                };

                bool hasContent = false;
                foreach (var child in listItem)
                {
                    if (child is Markdig.Syntax.ParagraphBlock p)
                    {
                        if (p.Inline != null)
                        {
                            foreach (var inline in p.Inline)
                            {
                                ProcessInline(para, inline, baseDir);
                                hasContent = true;
                            }
                        }
                    }
                    else if (child is Markdig.Syntax.FencedCodeBlock cb)
                    {
                        var code = GetCodeText(cb);
                        para.Inlines.Add(new Run(code)
                        {
                            FontFamily = new FontFamily("Consolas"),
                            FontSize = 12
                        });
                        hasContent = true;
                    }
                }

                if (hasContent)
                {
                    li.Blocks.Add(para);
                    list.ListItems.Add(li);
                }
            }
        }

        doc.Blocks.Add(list);
    }

    private void AddFencedCode(FlowDocument doc, Markdig.Syntax.FencedCodeBlock codeBlock)
    {
        var code = GetCodeText(codeBlock);
        if (string.IsNullOrEmpty(code)) return;

        var section = new Section();
        section.Background = ResolveBrush("CodeBlockBackgroundBrush", Color.FromRgb(24, 24, 24));
        section.BorderBrush = ResolveBrush("CodeBlockBorderBrush", Color.FromRgb(60, 60, 60));
        section.BorderThickness = new Thickness(1);
        section.Padding = new Thickness(12);
        section.Margin = new Thickness(0, 4, 0, 12);

        var para = new Paragraph
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            LineHeight = 1.3,
            Margin = new Thickness(0),
            Foreground = Foreground
        };

        para.Inlines.Add(new Run(code));
        section.Blocks.Add(para);
        doc.Blocks.Add(section);
    }

    private void AddSimpleCode(FlowDocument doc, Markdig.Syntax.CodeBlock codeBlock)
    {
        var code = GetCodeText(codeBlock);
        if (string.IsNullOrEmpty(code)) return;

        var section = new Section();
        section.Background = ResolveBrush("CodeBlockBackgroundBrush", Color.FromRgb(24, 24, 24));
        section.BorderBrush = ResolveBrush("CodeBlockBorderBrush", Color.FromRgb(60, 60, 60));
        section.BorderThickness = new Thickness(1);
        section.Padding = new Thickness(12);
        section.Margin = new Thickness(0, 4, 0, 12);

        var para = new Paragraph
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            LineHeight = 1.3,
            Margin = new Thickness(0),
            Foreground = Foreground
        };

        para.Inlines.Add(new Run(code));
        section.Blocks.Add(para);
        doc.Blocks.Add(section);
    }

    private void AddHorizontalRule(FlowDocument doc)
    {
        var border = new Border
        {
            Height = 1,
            Background = ResolveBrush("HorizontalRuleBrush", Color.FromRgb(60, 60, 60)),
            Margin = new Thickness(0, 8, 0, 8)
        };
        doc.Blocks.Add(new BlockUIContainer(border));
    }

    private void AddQuoteBlock(FlowDocument doc, Markdig.Syntax.QuoteBlock quoteBlock, string baseDir)
    {
        var section = new Section();
        section.BorderBrush = ResolveBrush("QuoteBorderBrush", Color.FromRgb(100, 100, 100));
        section.BorderThickness = new Thickness(3, 0, 0, 0);
        section.Padding = new Thickness(12, 4, 4, 4);
        section.Margin = new Thickness(0, 4, 0, 8);

        foreach (var child in quoteBlock)
        {
            if (child is Markdig.Syntax.ParagraphBlock p)
            {
                var para = new Paragraph
                {
                    Margin = new Thickness(0, 2, 0, 2),
                    Foreground = MutedForeground
                };
                if (p.Inline != null)
                {
                    foreach (var inline in p.Inline)
                        ProcessInline(para, inline, baseDir);
                }
                section.Blocks.Add(para);
            }
        }

        doc.Blocks.Add(section);
    }

    private void ProcessInline(Paragraph para, Markdig.Syntax.Inlines.Inline inline, string baseDir)
    {
        switch (inline)
        {
            case Markdig.Syntax.Inlines.LiteralInline literal:
                para.Inlines.Add(new Run(literal.Content.ToString()));
                break;

            case Markdig.Syntax.Inlines.LineBreakInline:
                para.Inlines.Add(new LineBreak());
                break;

            case Markdig.Syntax.Inlines.EmphasisInline emphasis:
                ProcessEmphasis(para, emphasis, baseDir);
                break;

            case Markdig.Syntax.Inlines.LinkInline link:
                ProcessLink(para, link, baseDir);
                break;

            case Markdig.Syntax.Inlines.CodeInline codeInline:
                var codeContainer = new InlineUIContainer(new Border
                {
                    Child = new TextBlock
                    {
                        Text = codeInline.Content,
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 12,
                        Foreground = new SolidColorBrush(Color.FromRgb(245, 166, 35)),
                    },
                    Padding = new Thickness(4, 1, 4, 1),
                    CornerRadius = new CornerRadius(3),
                    Background = ResolveBrush("InlineCodeBackgroundBrush", Color.FromRgb(40, 40, 40))
                });
                para.Inlines.Add(codeContainer);
                break;

            case Markdig.Syntax.Inlines.AutolinkInline autolink:
                var url = autolink.Url ?? "";
                var autoHyperlink = new Hyperlink(new Run(url))
                {
                    NavigateUri = new Uri(url),
                    Foreground = LinkForeground
                };
                autoHyperlink.RequestNavigate += (s, e) =>
                {
                    if (e.Uri.IsAbsoluteUri)
                        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
                    e.Handled = true;
                };
                para.Inlines.Add(autoHyperlink);
                break;
        }
    }

    private void ProcessEmphasis(Paragraph para, Markdig.Syntax.Inlines.EmphasisInline emphasis, string baseDir)
    {
        foreach (var child in emphasis)
        {
            if (child is Markdig.Syntax.Inlines.LiteralInline literal)
            {
                var text = literal.Content.ToString();
                if (emphasis.DelimiterCount >= 2)
                {
                    para.Inlines.Add(new Run(text)
                    {
                        FontWeight = FontWeights.Bold,
                        Foreground = Foreground
                    });
                }
                else
                {
                    para.Inlines.Add(new Run(text)
                    {
                        FontStyle = FontStyles.Italic,
                        Foreground = Foreground
                    });
                }
            }
            else if (child is Markdig.Syntax.Inlines.EmphasisInline nestedEmphasis)
            {
                ProcessEmphasis(para, nestedEmphasis, baseDir);
            }
            else if (child is Markdig.Syntax.Inlines.LinkInline nestedLink)
            {
                ProcessLink(para, nestedLink, baseDir);
            }
        }
    }

    private void ProcessLink(Paragraph para, Markdig.Syntax.Inlines.LinkInline link, string baseDir)
    {
        if (link.IsImage)
        {
            var url = link.Url ?? "";
            var alt = GetLinkText(link);
            var resolvedPath = ResolvePath(url, baseDir);
            if (!string.IsNullOrEmpty(resolvedPath) && File.Exists(resolvedPath))
            {
                try
                {
                    var img = new Image
                    {
                        Source = new BitmapImage(new Uri(resolvedPath)),
                        MaxWidth = 600,
                        Margin = new Thickness(0, 8, 0, 8)
                    };
                    para.Inlines.Add(new InlineUIContainer(img));
                }
                catch
                {
                    para.Inlines.Add(new Run($"[Imagen: {alt}]")
                    {
                        Foreground = ErrorForeground,
                        FontStyle = FontStyles.Italic
                    });
                }
            }
            else
            {
                para.Inlines.Add(new Run($"[{alt}]")
                {
                    Foreground = ErrorForeground,
                    FontStyle = FontStyles.Italic
                });
            }
        }
        else
        {
            var linkText = GetLinkText(link);
            var url = link.Url ?? "";
            if (!string.IsNullOrEmpty(url))
            {
                var hyperlink = new Hyperlink(new Run(linkText))
                {
                    NavigateUri = new Uri(url, UriKind.RelativeOrAbsolute),
                    Foreground = LinkForeground
                };
                hyperlink.RequestNavigate += (s, e) =>
                {
                    if (e.Uri.IsAbsoluteUri)
                        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
                    e.Handled = true;
                };
                para.Inlines.Add(hyperlink);
            }
            else
            {
                para.Inlines.Add(new Run(linkText));
            }
        }
    }

    private static string GetLinkText(Markdig.Syntax.Inlines.LinkInline link)
    {
        if (link.FirstChild is Markdig.Syntax.Inlines.LiteralInline literal)
            return literal.Content.ToString();
        return link.Url ?? "link";
    }

    private static string GetInlineText(Markdig.Syntax.LeafBlock block)
    {
        if (block.Inline == null) return "";
        var text = "";
        foreach (var inline in block.Inline)
        {
            if (inline is Markdig.Syntax.Inlines.LiteralInline literal)
                text += literal.Content.ToString();
            else if (inline is Markdig.Syntax.Inlines.CodeInline code)
                text += code.Content;
            else if (inline is Markdig.Syntax.Inlines.LineBreakInline)
                text += " ";
            else if (inline is Markdig.Syntax.Inlines.LinkInline link)
                text += GetLinkText(link);
            else if (inline is Markdig.Syntax.Inlines.EmphasisInline emp)
                text += GetEmphasisText(emp);
        }
        return text;
    }

    private static string GetEmphasisText(Markdig.Syntax.Inlines.EmphasisInline emphasis)
    {
        var text = "";
        foreach (var child in emphasis)
        {
            if (child is Markdig.Syntax.Inlines.LiteralInline literal)
                text += literal.Content.ToString();
            else if (child is Markdig.Syntax.Inlines.EmphasisInline nested)
                text += GetEmphasisText(nested);
        }
        return text;
    }

    private static string GetCodeText(Markdig.Syntax.LeafBlock codeBlock)
    {
        if (codeBlock.Lines.Count == 0) return "";
        var lines = codeBlock.Lines.Lines;
        if (lines == null) return "";

        var result = new System.Text.StringBuilder();
        for (int i = 0; i < lines.Length; i++)
        {
            var slice = lines[i];
            var text = slice.ToString();
            if (text.Length == 0 && i < lines.Length - 1) continue;
            if (result.Length > 0) result.Append('\n');
            result.Append(text);
        }
        return result.ToString();
    }

    private static string ResolvePath(string url, string baseDir)
    {
        if (string.IsNullOrEmpty(url)) return "";
        if (File.Exists(url)) return url;
        if (!string.IsNullOrEmpty(baseDir))
        {
            var combined = Path.Combine(baseDir, url);
            if (File.Exists(combined)) return combined;
        }
        return "";
    }

    // Resolved brushes from current application resources (theme-aware)
    private static Brush ResolveBrush(string key, Color fallback)
    {
        try
        {
            if (Application.Current?.TryFindResource(key) is SolidColorBrush brush)
                return brush;
        }
        catch { }
        return new SolidColorBrush(fallback);
    }

    private Brush Foreground => ResolveBrush("TextPrimaryBrush", Color.FromRgb(232, 232, 232));
    private Brush MutedForeground => ResolveBrush("TextMutedBrush", Color.FromRgb(136, 136, 136));
    private Brush LinkForeground => ResolveBrush("PrimaryBrush", Color.FromRgb(100, 180, 255));
    private Brush ErrorForeground => new SolidColorBrush(Color.FromRgb(200, 100, 100));
}
