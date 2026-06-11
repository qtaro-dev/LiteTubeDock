using System.Windows;
using LiteTubeDock.Constants;

namespace LiteTubeDock;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        Title = AppConstants.AboutWindowTitle;
        AppNameTextBlock.Text = AppConstants.AppName;
        VersionTextBlock.Text = AppConstants.VersionPrefix + AppConstants.AppVersion;
        DescriptionTextBlock.Text = HelpContent.AboutDescription;
        TechnologyHeadingTextBlock.Text = AppConstants.TechnologyHeadingText;
        TechnologyTextBlock.Text = HelpContent.TechnologyText;
        DeveloperTextBlock.Text = AppConstants.DeveloperDisplayText;
        DevelopmentSupportTextBlock.Text = AppConstants.DevelopmentSupportDisplayText;
        SecurityPolicyHeadingTextBlock.Text = AppConstants.SecurityPolicyHeadingText;
        SecurityPolicyTextBlock.Text = HelpContent.SecurityPolicyText;
        CloseAboutButton.Content = AppConstants.CloseButtonText;
        CloseAboutButton.Click += (_, _) => Close();
    }
}
