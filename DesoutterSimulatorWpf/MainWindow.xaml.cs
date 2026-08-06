using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DesoutterSimulatorWpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private bool _followLog = true;

        /// <summary>日志更新时：跟随模式（光标在最后一行/未手动移开）自动滚动到最新</summary>
        private void LogTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (sender is not System.Windows.Controls.TextBox tb) return;

            if (_followLog)
                tb.ScrollToEnd();
        }

        /// <summary>点击日志框时：点击最后一行（末尾空行）恢复自动滚动，点击其他位置停止滚动并显示回到底部按钮</summary>
        private void LogTextBox_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is not System.Windows.Controls.TextBox tb) return;

            int idx = tb.GetCharacterIndexFromPoint(e.GetPosition(tb), true);
            int lastLineStart = tb.Text.LastIndexOf('\n') + 1; // 末尾空行的起始位置
            _followLog = idx >= lastLineStart;
            UpdateLogFollowButton();
        }

        /// <summary>回到底部：恢复自动滚动并滚动到最新</summary>
        private void JumpToBottomButton_Click(object sender, RoutedEventArgs e)
        {
            _followLog = true;
            UpdateLogFollowButton();
            if (LogTextBox != null)
            {
                LogTextBox.CaretIndex = LogTextBox.Text.Length;
                LogTextBox.ScrollToEnd();
            }
        }

        /// <summary>跟随模式下隐藏回到底部按钮，停止跟随（点击其他位置）时显示</summary>
        private void UpdateLogFollowButton()
        {
            JumpToBottomButton.Visibility = _followLog ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}