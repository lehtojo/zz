using System;
using System.Linq;
using System.Collections.Generic;

public class File
{
	public string Fullname { get; private set; }
	public string Filename => System.IO.Path.GetFileName(Fullname);
	public string? Folder => System.IO.Path.GetDirectoryName(Fullname);
	public string Content { get; private set; }
	public int Index { get; private set; }
	public List<Token> Tokens { get; private set; }
	public Node? Root { get; set; }
	public Context? Context { get; set; }

	public File(string filename, string content, int index)
	{
		Fullname = filename;
		Content = content;
		Index = index;
		Tokens = new List<Token>();
	}

	public string GetFilenameWithoutExtension()
	{
		return System.IO.Path.GetFileNameWithoutExtension(Fullname);
	}

	public override bool Equals(object? other)
	{
		return other is File file && Fullname.Equals(file.Fullname);
	}

	public override int GetHashCode()
	{
		return Fullname.GetHashCode();
	}
}

public class FilePhase : Phase
{
	public const string OUTPUT = "files";

	private const char CARRIAGE_RETURN_CHARACTER = '\r';
	private const char TAB_CHARACTER = '\t';

	public override Status Execute(Bundle bundle)
	{
		var filenames = bundle.Get(ConfigurationPhase.FILES, Array.Empty<string>());

		if (!filenames.Any())
		{
			return Status.Error("Please enter input files");
		}

		var files = new File[filenames.Length];

		for (var i = 0; i < filenames.Length; i++)
		{
			var index = i;

			Run(() =>
			{
				var filename = filenames[index];

				try
				{
					var content = System.IO.File.ReadAllText(filename).Replace(CARRIAGE_RETURN_CHARACTER, ' ').Replace(TAB_CHARACTER, ' ');
					files[index] = new File(filename, content, index + 1);
				}
				catch
				{
					return Status.Error("Could not load file '{0}'", filename);
				}

				return Status.OK;
			});
		}

		bundle.Put(OUTPUT, files);

		return Status.OK;
	}
}