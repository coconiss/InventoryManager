// Views/UpdateDialog.xaml.cs
using InventoryManager.Services;
using System.Windows;

namespace InventoryManager.Views;

public partial class UpdateDialog : Window
{
    private readonly VersionInfo _info;
    private readonly bool _isMandatory;

    public bool UserAccepted { get; private set; }

    public UpdateDialog(VersionInfo info, Version current, bool isMandatory)
    {
        InitializeComponent();
        _info = info;
        _isMandatory = isMandatory;

        TitleText.Text = isMandatory ? "필수 업데이트" : "새 버전이 있습니다";
        VersionText.Text = $"현재 버전: {current}  →  최신 버전: {info.Version}";
        ReleaseNotesText.Text = info.ReleaseNotes;

        if (isMandatory)
        {
            MandatoryBanner.Visibility = Visibility.Visible;
            SkipButton.IsEnabled = false;  // 강제: 건너뛰기 비활성
        }
    }

    private void Update_Click(object sender, RoutedEventArgs e)
    {
        UserAccepted = true;
        Close();
    }

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        UserAccepted = false;
        Close();
    }

    // 강제 업데이트 시 창 닫기(X) 버튼 비활성
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_isMandatory && !UserAccepted)
            e.Cancel = true;
        base.OnClosing(e);
    }
}