using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TaskbarAiMonitor.Models;

public class MetricItem : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _name = string.Empty;
    private string _iconGlyph = string.Empty;
    private string _iconColorHex = "#FFFFFF";
    private int _percentage = 0;
    private string _text = "0%";
    private string _detail = string.Empty;
    private bool _isWarning = false;

    public string Id
    {
        get => _id;
        set => SetField(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public string IconGlyph
    {
        get => _iconGlyph;
        set => SetField(ref _iconGlyph, value);
    }

    public string IconColorHex
    {
        get => _iconColorHex;
        set => SetField(ref _iconColorHex, value);
    }

    public int Percentage
    {
        get => _percentage;
        set
        {
            if (SetField(ref _percentage, value))
            {
                Text = $"{_percentage}%";
            }
        }
    }

    public string Text
    {
        get => _text;
        set => SetField(ref _text, value);
    }

    public string Detail
    {
        get => _detail;
        set => SetField(ref _detail, value);
    }

    public bool IsWarning
    {
        get => _isWarning;
        set => SetField(ref _isWarning, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
