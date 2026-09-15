using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CodePacker.Models;

public class TreeNodeItem : INotifyPropertyChanged
{
    private bool? _isChecked = true;
    private bool _isExpanded = true;

    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public bool IsFolder { get; set; }
    public string? FileId { get; set; }
    public long SizeBytes { get; set; }
    public ObservableCollection<TreeNodeItem> Children { get; set; } = new();
    public TreeNodeItem? Parent { get; set; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set { _isExpanded = value; OnPropertyChanged(); }
    }

    public bool? IsChecked
    {
        get => _isChecked;
        set => SetChecked(value, true, true);
    }

    public void SetChecked(bool? value, bool updateChildren, bool updateParent)
    {
        if (_isChecked == value) return;
        _isChecked = value;
        OnPropertyChanged(nameof(IsChecked));

        if (updateChildren && _isChecked.HasValue && IsFolder)
        {
            foreach (var child in Children)
                child.SetChecked(_isChecked.Value, true, false);
        }

        if (updateParent && Parent != null)
        {
            Parent.VerifyCheckState();
        }
    }

    public void VerifyCheckState()
    {
        bool? state = null;
        for (int i = 0; i < Children.Count; i++)
        {
            var current = Children[i].IsChecked;
            if (i == 0)
            {
                state = current;
            }
            else if (state != current)
            {
                state = null;
                break;
            }
        }
        SetChecked(state, false, true);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}