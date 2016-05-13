using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

/*                                                ______           
                                     ___.--------'------`---------.____ 
                               _.---'----------------------------------`---.__
                             .'___=]============================================
,-----------------------..__/.'         >--.______        _______.----'
]====================<==||(__)        .'          `------'
`-----------------------`' ----.___--/
     /       /---'                 `/         Anthony J. Mills
    /_______(______________________/             JavaScript
    `-------------.--------------.'              Identifier
                   \________|_.-'                Obfuscator

Syntax:
  under *.js *.css *.cshtml
    Processes all js, css, and cshtml in the current directory and those under it.

This program takes text files and replaces all words in them that end with an underline (_) with
shorter identifiers (not starting with numbers) that were not present as words in the original
text file.

This is generally useful for taking a file of JavaScript source code where the identifiers to be
obfuscated start with an underline. In that context, this script will produce a source code file
that behaves identically.

Typical JavaScript compilation pipeline:
copy site.html prod\site.html
copy site.css prod\site.css
copy/b scripts\engine.js + scripts\app.js temp\all.js
ajaxmin -clobber temp\all.js -o prod\all.js
cscript //nologo build\under.js prod\all.js prod\site.html prod\site.css
del temp\all.js

If letters are not specified, the replacement identifiers use letters chosen from the non-
replaced words in the source text in order of frequency. This allows the final product to be
gzipped into a slightly smaller package.

This program fills a gap left by minification tools. They can't minify or obfuscate object
members. So in your source code, anything that they will minify (like arguments and variable
names) you leave alone; stuff that is safe to minify (internal class names, internal function
and member names, etc.) you add an underscore to.

You can use it for CSS classes and such too as long as they're only referred to by resources
that are dynamically compiled. So, for example, .cshtml files; if you refer to CSS classes from
server code, it's best to leave those classes un-underscored.
*/

namespace Under {
	public static class Extensions {
		public static TValue Get<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key) {
			TValue val;
			return dict.TryGetValue(key, out val) ? val : default(TValue);
		}
	}

	static class LinqExtensions {
		public static TResult[] Map<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult> transformer) {
			if (source == null) return new TResult[0];
			var list = source as IList<TSource>;
			if (list != null) {
				var count = list.Count;
				if (count == 0) return new TResult[0];
				var result = new TResult[count];
				for (var index = 0; index < count; index++)
					result[index] = transformer(list[index]);
				return result;
			} else {
				var collection = source as ICollection<TSource>;
				var count = collection != null ? collection.Count : source.Count();
				if (count == 0) return new TResult[0];
				var result = new TResult[count];
				var resultIndex = 0; foreach (var item in source) result[resultIndex++] = transformer(item);
				return result;
			}
		}
	}

	struct LetterFrequency {
		public int Frequency;
		public char Letter;
	}

	struct WordReplace {
		public int Frequency;
		public string Original;
		public string Replacement;
	}

	class MainClass {
		/// <summary>
		/// Returns the num'th identifier that you can make using the array letters which details which
		/// letters can be used at each position. If letters was ["gG", "oO", "gG"], you would get:
		/// g, G, go, Go, gO, GO, gog, Gog, gOg, GOg, goG, GoG, gOG, GOG, gogo, Gogo, gOgo, GOgo, ...
		/// Since the first letter cannot contain a number, we loop back to the second position when
		/// we run out of letters; it's generally assumed that the last element of the array will be
		/// the same as the first, but possibly including numbers.
		/// </summary>
		static string IdentifierFromLetters(int num, string[] letters) {
			var letter = 0;
			var result = "";
			for (;;) {
				var choices = letters[letter].Length;
				result += letters[letter][num % choices];
				num = num / choices;
				if (num == 0) break;
				--num;
				++letter;
				if (letter == letters.Length) letter = 1;
			}
			return result;
		}

		public static void Main(string[] args) {
			var matchesPattern = new Regex(".+_$", RegexOptions.Compiled);
			var possiblePattern = new Regex(@"[0-9A-Za-z_\$]+");
			var illegalBeginningCharPattern = new Regex("[0-9]");

			var fileNames = new List<string>();
			string[] letters = null;
			var help = args.Length == 0;

			// Parse command-line arguments; get the letters to use for replacements and the list of files to modify
			for (int index = 0, length = args.Length; index < length; ++index) {
				var arg = args[index];
				var larg = arg.ToLowerInvariant();
				if (larg == "-l" || larg == "/l") {
					++index;
					letters = args[index].Split(',');
					Array.Resize(ref letters, letters.Length + 1);
					letters[letters.Length - 1] = illegalBeginningCharPattern.Replace(letters[0], "");
				} else if (larg == "-?" || larg == "/h") {
					help = true;
				} else if (arg.Contains("*") || arg.Contains("?")) {
					var rawPath = Path.GetDirectoryName(arg);
					var path = string.IsNullOrEmpty(rawPath) ? "." : rawPath;
					var searchPattern = Path.GetFileName(arg);
					var names = Directory.GetFiles(path, searchPattern, SearchOption.AllDirectories);
					fileNames.AddRange(names);
				} else {
					fileNames.Add(arg);
				}
			}

			Console.WriteLine("Under Identifier Obfuscator (c) 2010-2016 by Anthony J. Mills");
			Console.WriteLine("Open Source, MIT license @ http://github");
			Console.WriteLine();

			if (help) {
				Console.WriteLine("Replaces common identifiers ending in an underscore across a set of files with shorter versions.");
				Console.WriteLine();
				Console.WriteLine("Syntax:");
				Console.WriteLine("  under filespec [filespec ...] [-l letters]");
				Console.WriteLine();
				Console.WriteLine("Where:");
				Console.WriteLine("  filespec is either the name of a file or a search pattern to be searched recursively");
				Console.WriteLine("  letters are letters to be used in identifier creation e.g. vV,aA4,rR,iI1,aA4,bB,lL1,eE3,sS5");
				Console.WriteLine();
				Console.WriteLine("If you don't specify letters, letters will be calculated to try to maximize compressibility.");
				Console.WriteLine();
				Console.WriteLine("Examples:");
				Console.WriteLine("  under *.js *.css *.cshtml");
				Console.WriteLine("  under scripts/*.js styles/*.css views/*.cshtml -l aA4,jJ,mM");
				return;
			}

			Console.WriteLine("Files in set: " + fileNames.Count);

			// Get all file contents
			var fileContents = fileNames.Map(n => File.ReadAllText(n));
			var contents = string.Join(" ", fileContents);
			var wordsCollection = possiblePattern.Matches(contents);
			var words = new string[wordsCollection.Count];
			for (int index = 0, length = wordsCollection.Count; index < length; ++index) words[index] = wordsCollection[index].Value;

			// Get word frequency of _ words and letter frequency distribution of non-_ words
			var matchFreq = new Dictionary<string, int>();
			var otherExists = new Dictionary<string, bool>();
			var otherLetterFrequency = new Dictionary<char, int>();
			foreach (var word in words) {
				if (matchesPattern.IsMatch(word)) {
					matchFreq[word] = matchFreq.Get(word) + 1;
				} else {
					otherExists[word] = true;
					if (letters == null) {
						foreach (var ch in word) {
							if ((ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z'))
								otherLetterFrequency[ch] = otherLetterFrequency.Get(ch) + 1;
						}
					}
				}
			}

			Console.WriteLine("Unique words in the set: " + (otherExists.Count + matchFreq.Count));
			Console.WriteLine("Words targeted for replacement: " + matchFreq.Count);

			// Fake that JavaScript reserved words exist already to prevent replacements using these accidentally
			// List retrieved from https://developer.mozilla.org/en/JavaScript/Reference/Reserved_Words
			var reservedWords = ("break,case,catch,continue,default,delete,do,else,finally,for,function,if,in,instanceof," +
				"new,return,switch,this,throw,try,typeof,var,void,while,with,abstract,boolean,byte,char,class,const," +
				"debugger,double,enum,export,extends,final,float,goto,implements,import,int,interface,long,native," +
				"package,private,protected,public,short,static,super,synchronized,throws,transient,volatile").Split(',');
			foreach (var reservedWord in reservedWords) {
				otherExists[reservedWord] = true;
			}

			// Build the letters array to use for identifiers by populating it with the most popular letters
			// Ignore this section if letters have already been specified with -l
			if (letters == null) {
				// Count letters and put letter frequency in an array so it can be sorted
				var letterArr = otherLetterFrequency.Map(kv => new LetterFrequency { Frequency = kv.Value, Letter = kv.Key });

				// Sort the letter frequency array from highest to lowest letter frequency
				Array.Sort(letterArr, (a, b) => b.Frequency - a.Frequency);

				// Make up a letterset consisting of the letters that make up 80% of the used letters e.g. "aaaab" => "a"
				var bestLetters = "";
				for (int index = 0, total = 0, ideal = letterArr.Sum(lf => lf.Frequency) * 4 / 5; total < ideal; ++index) {
					bestLetters += letterArr[index].Letter;
					total += letterArr[index].Frequency;
				}
				letters = new[] { illegalBeginningCharPattern.Replace(bestLetters, ""), bestLetters };
			}

			Console.WriteLine("Using letters: " + string.Join(" ", letters));

			// Move word frequency distribution into an array so it can be sorted
			var replacementWork = matchFreq.Map(kv => new WordReplace { Frequency = kv.Value, Original = kv.Key, Replacement = null });

			// Sort the array from most frequent word we're replacing to least frequent
			Array.Sort(replacementWork, (a, b) => b.Frequency - a.Frequency);

			// Give each original word a unique replacement that did not exist in the original
			// By assigning replacements from most frequent to least, we give more frequent identifiers shorter replacements
			for (int index = 0, counter = 0, length = replacementWork.Length; index < length; ++index) {
				string replacement;
				do {
					replacement = IdentifierFromLetters(counter, letters);
					++counter;
				} while (otherExists.ContainsKey(replacement));
				replacementWork[index].Replacement = replacement;
			}

			Console.WriteLine("Most frequent words:" + string.Join(
				"", replacementWork.Select(r => "\n  " + r.Frequency + " x " + r.Original + " => " + r.Replacement).Take(5)));

			// Sort array by descending length (so we replace long strings before short strings)
			// If we accidentally replaced "zig_" before "zig_ging_" then it would break identifiers
			Array.Sort(replacementWork, (a, b) => b.Original.Length - a.Original.Length);

			// Replace everything by its designated replacement
			foreach (var item in replacementWork) {
				var original = new Regex(@"\b" + item.Original.Replace("$", @"\$") + @"\b");
				for (int index = 0, len = fileContents.Length; index < len; ++index) {
					fileContents[index] = original.Replace(fileContents[index], item.Replacement);
				}
			}

			// Go through each file and write out
			for (int index = 0, length = fileNames.Count; index < length; ++index) {
				File.WriteAllText(fileNames[index], fileContents[index]);
			}
		}
	}
}
