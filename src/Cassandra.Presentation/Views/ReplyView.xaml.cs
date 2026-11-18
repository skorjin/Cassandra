using System.Windows.Controls;
using System.Windows.Input;

namespace Cassandra.Presentation.Views;

public partial class ReplyView : UserControl
{
    public ReplyView() => InitializeComponent();

    public void FocusInput()
    {
        InputBox.Focus();
        Keyboard.Focus(InputBox);
        InputBox.SelectAll();
    }
}
