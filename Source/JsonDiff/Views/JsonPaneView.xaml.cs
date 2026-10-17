using System.Windows.Controls;

namespace JsonDiff.Views;

/// <summary>Read-only, virtualized list of one side's diff lines; the DataContext is a <c>FilePaneViewModel</c>.</summary>
public partial class JsonPaneView : UserControl
{
    public JsonPaneView()
    {
        InitializeComponent();
    }
}
