using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace LazyLineReader;

public sealed class MainWindowModel : INotifyPropertyChanged, IDisposable
{
    public const int MaxLines = 20;

    private Stream? stream;
    private MatchCollection? matches;
    private int matchIndex;

    public MainWindowModel()
    {
        Items.CollectionChanged += (sender, e) =>
        {
            var sb = new StringBuilder(Items.Sum(x => x.Length + Environment.NewLine.Length));

            foreach (string line in Items)
                sb.AppendLine(line);

            Text = sb.ToString();
        };
    }

    public string? FilePath
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public long? CurrentLineNumber
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CurrentStartLineNumber));
            OnPropertyChanged(nameof(EndOfFile));
        }
    }

    public long? CurrentStartLineNumber => CurrentLineNumber.HasValue ? CurrentLineNumber - Items.Count + 1 : null;

    public bool EndOfFile => Reader != null && Reader.EndOfStream;

    public Encoding? Encoding => Reader?.CurrentEncoding;

    public ObservableDeque<string> Items { get; } = new(MaxLines);

    public string Text
    {
        get;
        set
        {
            field = value;
            matches = null;
            matchIndex = 0;

            OnPropertyChanged();
        }
    } = string.Empty;

    [MemberNotNull(nameof(CurrentLineNumber))]
    private StreamReader? Reader { get; set; }

    public void Open(Stream stream)
    {
        Items.Clear();
        Dispose();

        this.stream = stream;
        Reader = new StreamReader(stream, true);

        for (CurrentLineNumber = 0; CurrentLineNumber < MaxLines; CurrentLineNumber++)
        {
            if (Reader.EndOfStream)
                break;

            Items.AddLast(Reader.ReadLine()!);
        }
        OnPropertyChanged(nameof(Encoding));
    }

    public void ReadNext(int lines)
    {
        if (Reader == null)
            return;

        for (int i = 0; i < lines; i++)
        {
            if (Reader.EndOfStream)
                break;

            Items.AddLast(Reader.ReadLine()!);
            CurrentLineNumber++;
        }
    }

    public void Dispose()
    {
        Reader?.Close();
        Reader = null;

        stream?.Close();
        stream = null;
    }

    public Match? Search(string? searchPattern)
    {
        if (Reader != null && searchPattern != null)
        {
            if (matches != null)
            {
                if (matchIndex < matches.Count - 1)
                {
                    matchIndex++;
                    return matches[matchIndex];
                }
            }
            else
            {
                var regex = CreateRegex(searchPattern);
                matches = regex.Matches(Text);

                if (matches.Count > 0)
                {
                    matchIndex = 0;
                    return matches[matchIndex];
                }
            }
        }
        matches = null;
        matchIndex = 0;
        return null;
    }

    public Match? ReadAndSearch(string? searchPattern)
    {
        if (Reader == null || searchPattern == null)
        {
            matches = null;
            matchIndex = 0;
            return null;
        }

        var regex = CreateRegex(searchPattern);
        var clone = Items.Clone();
        bool matched = false;
        long readLineNumber = CurrentLineNumber.Value;

        while (!Reader.EndOfStream)
        {
            clone.AddLast(Reader.ReadLine()!);
            CurrentLineNumber++;

            if (regex.IsMatch(clone[^1]))
            {
                matched = true;
                break;
            }
        }

        Items.CopyFrom(clone);

        if (matched)
        {
            if (readLineNumber < CurrentStartLineNumber)
                return Search(searchPattern);

            matches = regex.Matches(Text, Text.IndexOf(Items.Last(), StringComparison.Ordinal));
            matchIndex = 0;
            return matches[matchIndex];
        }
        else
        {
            matches = null;
            matchIndex = 0;
            return null;
        }
    }

    public Task<Match?> SearchAsync(string searchPattern, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(searchPattern);

        return new Task<Match?>(() =>
        {
            if (Reader != null && searchPattern != null)
            {
                if (matches != null)
                {
                    if (matchIndex < matches.Count - 1)
                    {
                        matchIndex++;
                        return matches[matchIndex];
                    }
                }
                else
                {
                    var regex = CreateRegex(searchPattern);
                    matches = regex.Matches(Text);

                    if (matches.Count > 0)
                    {
                        matchIndex = 0;
                        return matches[matchIndex];
                    }
                }
            }
            matches = null;
            matchIndex = 0;
            return null;

        }, cancellationToken);
    }

    public Task<Match?> ReadAndSearchAsync(string searchPattern, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(searchPattern);

        var context = TaskScheduler.FromCurrentSynchronizationContext();
        var clone = Items.Clone();
        var cloneLineNumber = CurrentLineNumber;

        var task = new Task<Match?>(() =>
        {
            if (Reader == null || searchPattern == null)
            {
                matches = null;
                matchIndex = 0;
                return null;
            }

            var regex = CreateRegex(searchPattern);
            bool matched = false;

            while (!Reader.EndOfStream)
            {
                clone.AddLast(Reader.ReadLine()!);
                cloneLineNumber++;

                if (regex.IsMatch(clone[^1]))
                {
                    matched = true;
                    break;
                }
            }
            matches = null;
            matchIndex = 0;

            Task.Factory.StartNew(() =>
            {
                CurrentLineNumber = cloneLineNumber;
                Items.CopyFrom(clone);

            }, cancellationToken, TaskCreationOptions.None, context).Wait();

            return matched ? Search(searchPattern) : null;

        }, cancellationToken);

        return task;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static Regex CreateRegex(string searchPattern)
    {
        return new Regex(searchPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
