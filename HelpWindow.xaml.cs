using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LiteTubeDock.Constants;

namespace LiteTubeDock;

public partial class HelpWindow : Window
{
    public HelpWindow()
    {
        InitializeComponent();

        Title = AppConstants.HelpWindowTitle;
        HelpContentTextBlock.Text = HelpContent.HelpText;
        BuildHelpContent();
        CloseHelpButton.Content = AppConstants.CloseButtonText;
        CloseHelpButton.Click += (_, _) => Close();
    }

    private void BuildHelpContent()
    {
        HelpContentPanel.Children.Clear();
        AddLogoHeader();
        AddTableOfContents();

        foreach (var section in HelpContent.Sections)
        {
            AddSeparator();
            AddSection(section);
        }
    }

    private void AddLogoHeader()
    {
        HelpContentPanel.Children.Add(new System.Windows.Controls.Image
        {
            Source = new BitmapImage(new Uri("pack://application:,,,/Resource/Images/LiteTubeDock_LogoLockup.png")),
            MaxWidth = 360,
            Height = 78,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 18)
        });
    }

    private void AddTableOfContents()
    {
        HelpContentPanel.Children.Add(CreateSelectableText(
            "目次",
            fontSize: 17,
            fontWeight: FontWeights.Bold,
            margin: new Thickness(0, 0, 0, 8)));

        foreach (var item in HelpContent.TableOfContents)
        {
            AddBullet(item, new Thickness(8, 0, 0, 4));
        }
    }

    private void AddSection(HelpContent.HelpSection section)
    {
        HelpContentPanel.Children.Add(CreateSelectableText(
            section.Heading,
            fontSize: 18,
            fontWeight: FontWeights.Bold,
            margin: new Thickness(0, 0, 0, 10)));

        foreach (var paragraph in section.Paragraphs)
        {
            AddParagraph(paragraph);
        }

        foreach (var bullet in section.Bullets)
        {
            AddBullet(bullet, new Thickness(8, 0, 0, 6));
        }

        foreach (var note in section.Notes)
        {
            AddNote(note);
        }
    }

    private void AddParagraph(string text)
    {
        HelpContentPanel.Children.Add(CreateSelectableText(
            text,
            fontSize: 14,
            fontWeight: FontWeights.Normal,
            margin: new Thickness(0, 0, 0, 8)));
    }

    private void AddBullet(string text, Thickness margin)
    {
        HelpContentPanel.Children.Add(CreateSelectableText(
            "・" + text,
            fontSize: 14,
            fontWeight: FontWeights.Normal,
            margin: margin));
    }

    private void AddNote(string text)
    {
        HelpContentPanel.Children.Add(new Border
        {
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 248, 220)),
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 190, 120)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 4, 0, 10),
            Child = CreateSelectableText(
                text,
                fontSize: 14,
                fontWeight: FontWeights.SemiBold,
                margin: new Thickness(0))
        });
    }

    private static System.Windows.Controls.TextBox CreateSelectableText(
        string text,
        double fontSize,
        FontWeight fontWeight,
        Thickness margin)
    {
        return new System.Windows.Controls.TextBox
        {
            Text = text,
            IsReadOnly = true,
            BorderThickness = new Thickness(0),
            Background = System.Windows.Media.Brushes.Transparent,
            FontSize = fontSize,
            FontWeight = fontWeight,
            TextWrapping = TextWrapping.Wrap,
            Margin = margin,
            Padding = new Thickness(0),
            IsTabStop = false,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
            VerticalContentAlignment = System.Windows.VerticalAlignment.Top
        };
    }

    private void AddSeparator()
    {
        HelpContentPanel.Children.Add(new Border
        {
            Height = 1,
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(210, 210, 210)),
            Margin = new Thickness(0, 16, 0, 16)
        });
    }
}
