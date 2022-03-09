using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace LazyLineReader;

public class MainWindowModel : INotifyPropertyChanged
{
	public const int MaxLines = 20;

	private string? filePath;
	private Stream? stream;
	private StreamReader? reader;
	private long? lineNumber;
	private readonly Deque<string> items = new(MaxLines);
	private string text = string.Empty;
	private MatchCollection? matches;
	private int matchIndex;

	public MainWindowModel()
	{
		items.CollectionChanged += (sender, e) =>
		{
			var sb = new StringBuilder(items.Sum(x => x.Length + Environment.NewLine.Length));

			foreach (string line in items)
				sb.Append(line).Append(Environment.NewLine);

			Text = sb.ToString();
		};
	}

	public string? FilePath
    {
        get => filePath;
        set
        {
            filePath = value;
            OnPropertyChanged();
        }
    }

    public long? CurrentLineNumber
    {
        get => lineNumber;
        set
        {
            lineNumber = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CurrentStartLineNumber));
            OnPropertyChanged(nameof(EndOfFile));
        }
    }

    public long? CurrentStartLineNumber => lineNumber.HasValue ? lineNumber - items.Count + 1 : null;

    public bool EndOfFile => reader != null && reader.EndOfStream;

    public Encoding? Encoding => reader?.CurrentEncoding;

    public Deque<string> Items => items;

    public string Text
    {
        get => text;
        set
        {
            text = value;
            matches = null;
            matchIndex = 0;

            OnPropertyChanged();
        }
    }

    public void Open(Stream stream)
	{
		Items.Clear();
		Close();

		this.stream = stream;
		reader = new StreamReader(stream, true);

		for (CurrentLineNumber = 0; CurrentLineNumber < MaxLines; CurrentLineNumber++)
		{
			if (reader.EndOfStream)
				break;

			items.AddLast(reader.ReadLine()!);
		}
		OnPropertyChanged(nameof(Encoding));
	}

	public void ReadNext(int lines)
	{
		if (reader == null)
			return;

		for (int i = 0; i < lines; i++)
		{
			if (reader.EndOfStream)
				break;

			items.AddLast(reader.ReadLine()!);
			CurrentLineNumber++;
		}
	}

	public void Close()
	{
		if (reader != null)
		{
			reader.Close();
			reader = null;
		}

		if (stream != null)
		{
			stream.Close();
			stream = null;
		}
	}

	public Match? Search(string? searchPattern)
	{
		if (reader != null && searchPattern != null)
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
		if (reader == null || searchPattern == null)
		{
			matches = null;
			matchIndex = 0;
			return null;
		}

		var regex = CreateRegex(searchPattern);
		var clone = items.Clone();
		bool matched = false;
		long readLineNumber = CurrentLineNumber.Value;

		while (!reader.EndOfStream)
		{
			clone.AddLast(reader.ReadLine()!);
			CurrentLineNumber++;

			if (regex.IsMatch(clone[^1]))
			{
				matched = true;
				break;
			}
		}

		items.CopyFrom(clone);

		if (matched)
		{
			if (readLineNumber < CurrentStartLineNumber)
				return Search(searchPattern);

			matches = regex.Matches(Text, Text.IndexOf(items.Last(), StringComparison.Ordinal));
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
		if (searchPattern == null)
			throw new ArgumentNullException(nameof(searchPattern));

		return new Task<Match?>(() =>
		{
			if (reader != null && searchPattern != null)
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
		if (searchPattern == null)
			throw new ArgumentNullException(nameof(searchPattern));

		var context = TaskScheduler.FromCurrentSynchronizationContext();
		var clone = items.Clone();
		var cloneLineNumber = CurrentLineNumber;

		var task = new Task<Match?>(() =>
		{
			if (reader == null || searchPattern == null)
			{
				matches = null;
				matchIndex = 0;
				return null;
			}

			var regex = CreateRegex(searchPattern);
			bool matched = false;

			while (!reader.EndOfStream)
			{
				clone.AddLast(reader.ReadLine()!);
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
				items.CopyFrom(clone);

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
