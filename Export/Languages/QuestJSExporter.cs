using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Trizbort.Automap;
using Trizbort.Domain;
using Trizbort.Domain.Application;
using Trizbort.Domain.Elements;
using Trizbort.Domain.Enums;
using Trizbort.Domain.Misc;
using Trizbort.Export.Domain;
using Trizbort.Extensions;

namespace Trizbort.Export.Languages {
  internal class QuestJSExporter : CodeExporter {
    private const char SINGLE_QUOTE = '\'';
    private const char DOUBLE_QUOTE = '"';
    private const char SPACE = ' ';
    private const char CURLY_OPEN = '{';

    public override List<KeyValuePair<string, string>> FileDialogFilters => new List<KeyValuePair<string, string>> {
      new KeyValuePair<string, string>("QuestJS Source File", ".js"),
      new KeyValuePair<string, string>("Text Files", ".txt")
    };

    public override string FileDialogTitle => "Export QuestJS Source Code";

    protected override IEnumerable<string> ReservedWords => new[] {"object", "objects"};

    protected override void ExportContent(TextWriter writer) {
      // export location
      bool needConditionalFunction = false, wroteConditionalFunction = false;
      foreach (var location in LocationsInExportOrder) {
        writer.WriteLine();
        writer.WriteLine($"createRoom(\"{location.ExportName}\", {CURLY_OPEN}");
        if (!String.IsNullOrWhiteSpace(location.Room.PrimaryDescription)) {
          writer.WriteLine();
          writer.Write($"  desc:{toQuestJSString(location.Room.PrimaryDescription)},");
        }

        foreach (var direction in Directions.AllDirections) {
          var exit = location.GetBestExit(direction);
          if (exit != null) {
            writer.WriteLine();
            writer.Write($"  {toQuestJSPropertyName(direction)}: \"{exit.Target.ExportName}\",");
            var oppositeDirection = CompassPointHelper.GetOpposite(direction);
            if (Exit.IsReciprocated(location, direction, exit.Target)) {
              var reciprocal = exit.Target.GetBestExit(oppositeDirection);
              reciprocal.Exported = true;
            }
          }
        }

        if (location.Room.IsDark) {
          writer.WriteLine();
          writer.Write("  dark:true,");
        }

        writer.WriteLine("})");
        writer.WriteLine();


        exportThings(writer, location.Things, null, 1);
      }
    }


    protected override void ExportHeader(TextWriter writer, string title, string author, string description, string history) {
      var list = Project.Current.Elements.OfType<Room>().Where(p => p.IsStartRoom).ToList();
      var startingRoom = list.Count == 0 ? LocationsInExportOrder.First() : LocationsInExportOrder.Find(p => p.Room.ID == list.First().ID);

      writer.WriteLine($"/*{title} - exported by Trizbort */");
      writer.WriteLine();


      writer.WriteLine($"/*Objects*/");
    }


    protected override string GetExportName(Room room, int? suffix) {
      var name = room.Name.ToUpper().Replace(' ', '_');
      if (suffix != null || containsWord(name, ReservedWords) || containsOddCharacters(name)) name = stripOddCharacters(name.Replace(" ", "-"));
      if (suffix != null) name = $"{name}_{suffix}";

      return name;
    }

    protected override string GetExportName(string displayName, int? suffix) {
      var name = stripOddCharacters(displayName);

      name = name.ToUpper().Replace(' ', '_');

      if (String.IsNullOrEmpty(name)) name = "item";
      if (suffix != null) name = $"{name}{suffix}";
      return name;
    }

    private static bool containsOddCharacters(string text) {
      return text.Any(c => c != ' ' && c != '_' && !char.IsLetterOrDigit(c));
    }

    private static bool containsWord(string text, IEnumerable<string> words) {
      return words.Any(word => containsWord(text, word));
    }

    private static bool containsWord(string text, string word) {
      if (String.IsNullOrEmpty(text)) return String.IsNullOrEmpty(word);
      var words = text.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);
      return words.Any(wordFound => StringComparer.InvariantCultureIgnoreCase.Compare(word, wordFound) == 0);
    }


    private static void exportThings(TextWriter writer, List<Thing> things, Thing container, int indent) {
      foreach (var thing in things.Where(p => p.Container == container)) {
        writer.WriteLine();
        writer.WriteLine($"createItem(\"{thing.ExportName}\",{getFlags(thing)}, {CURLY_OPEN}");

        if (thing.Container == null)
          writer.WriteLine($"  loc:\"{thing.Location.ExportName}\",");
        else
          writer.WriteLine($"  loc:\"{thing.Container.ExportName}\",");

        var words = getObjectWords(thing);
        if (words.Count > 0) writer.WriteLine($"  synonyms:[\"{words[words.Count - 1]}\"],");
        if (words.Count > 1) writer.WriteLine($"  synonyms:['{String.Join($"',{SPACE}'", words.Take(words.Count - 1))}'],");

        
        writer.WriteLine("})");

        if (thing.Contents.Any())
          exportThings(writer, thing.Contents, thing, indent++);
      }
    }

    private static string getFlags(Thing thing) {
      var flags = new StringBuilder("TAKEABLE(),");

      if (thing.Contents.Any()) flags.Append(" CONTAINER(false),");

      return flags.ToString();
    }

    private static IList<string> getObjectWords(Thing thing) {
      var synonyms = String.Empty;
      var list = new List<string>();

      var words = thing.DisplayName.Split(' ').ToList();

      words.ForEach(p => list.Add(stripOddCharacters(p).ToUpper()));

      return list;
    }

    private static string stripOddCharacters(string text, params char[] exceptChars) {
      var exceptCharsList = new List<char>(exceptChars);
      var newText = text.Where(c => c == ' ' || c == '_' || char.IsLetterOrDigit(c) || exceptCharsList.Contains(c)).Aggregate(String.Empty, (current, c) => current + c);
      return String.IsNullOrEmpty(newText) ? "object" : newText;
    }

    private static string toQuestJSPropertyName(MappableDirection direction) {
      switch (direction) {
        case MappableDirection.North:
          return "north";
        case MappableDirection.South:
          return "south";
        case MappableDirection.East:
          return "east";
        case MappableDirection.West:
          return "west";
        case MappableDirection.NorthEast:
          return "northeast";
        case MappableDirection.SouthEast:
          return "southeast";
        case MappableDirection.SouthWest:
          return "southwest";
        case MappableDirection.NorthWest:
          return "northwest";
        case MappableDirection.Up:
          return "up";
        case MappableDirection.Down:
          return "down";
        case MappableDirection.In:
          return "in";
        case MappableDirection.Out:
          return "out";
        default:
          throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
      }
    }

    private static string toQuestJSString(string str) {
      if (str == null) str = String.Empty;
      return DOUBLE_QUOTE + str.Replace('\n', '|').Replace($"{DOUBLE_QUOTE}", $"\\{DOUBLE_QUOTE}") + DOUBLE_QUOTE;
    }
  }
}