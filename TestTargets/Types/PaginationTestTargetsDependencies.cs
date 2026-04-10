// Phase 10 pagination fixture sibling file — Dependencies scenario.
// DependencyKitchenSink method bodies reference >=105 distinct framework members
// so find_dependencies pagination can be exercised at the 100/105 boundary.
//
// Do not add/remove methods without updating the pagination assertions in
// Tests/Tools/FindDependenciesToolTests.cs Pagination_* facts.

#pragma warning disable CS0219  // Variable assigned but never used
#pragma warning disable CS8600  // Converting null literal
#pragma warning disable CS8602  // Dereference of a possibly null reference
#pragma warning disable CS8604  // Possible null reference argument

namespace ILSpy.Mcp.TestTargets.Pagination.Dependencies
{
    // Kitchen-sink class — find_dependencies against DependencyKitchenSink (no method filter)
    // returns >=105 distinct outward references (method calls, field accesses, type references).
    public class DependencyKitchenSink
    {
        // --- Console methods ---
        public void Dep001() { System.Console.WriteLine("a"); }
        public void Dep002() { System.Console.Write("b"); }
        public void Dep003() { System.Console.ReadLine(); }
        public void Dep004() { System.Console.ReadKey(); }
        public void Dep005() { System.Console.Clear(); }
        public void Dep006() { System.Console.Beep(); }
        public void Dep007() { var _ = System.Console.In; }
        public void Dep008() { var _ = System.Console.Out; }
        public void Dep009() { var _ = System.Console.Error; }
        public void Dep010() { System.Console.SetCursorPosition(0, 0); }

        // --- String methods ---
        public void Dep011() { var _ = string.Concat("a", "b"); }
        public void Dep012() { var _ = string.IsNullOrEmpty("a"); }
        public void Dep013() { var _ = string.IsNullOrWhiteSpace("a"); }
        public void Dep014() { var _ = string.Join(",", new[] { "a" }); }
        public void Dep015() { var _ = string.Compare("a", "b"); }
        public void Dep016() { var _ = string.Format("{0}", "a"); }
        public void Dep017() { var _ = "abc".Substring(0, 1); }
        public void Dep018() { var _ = "abc".Contains("a"); }
        public void Dep019() { var _ = "abc".StartsWith("a"); }
        public void Dep020() { var _ = "abc".EndsWith("c"); }
        public void Dep021() { var _ = "abc".IndexOf("b"); }
        public void Dep022() { var _ = "abc".Replace("a", "d"); }
        public void Dep023() { var _ = " abc ".Trim(); }
        public void Dep024() { var _ = "abc".ToUpper(); }
        public void Dep025() { var _ = "ABC".ToLower(); }
        public void Dep026() { var _ = "abc".Split(','); }
        public void Dep027() { var _ = "abc".ToCharArray(); }
        public void Dep028() { var _ = "abc".PadLeft(10); }
        public void Dep029() { var _ = "abc".PadRight(10); }
        public void Dep030() { var _ = "abc".Insert(0, "x"); }

        // --- Math methods ---
        public void Dep031() { var _ = System.Math.Abs(-1); }
        public void Dep032() { var _ = System.Math.Max(1, 2); }
        public void Dep033() { var _ = System.Math.Min(1, 2); }
        public void Dep034() { var _ = System.Math.Sqrt(4.0); }
        public void Dep035() { var _ = System.Math.Floor(1.5); }
        public void Dep036() { var _ = System.Math.Ceiling(1.5); }
        public void Dep037() { var _ = System.Math.Round(1.5); }
        public void Dep038() { var _ = System.Math.Pow(2, 3); }
        public void Dep039() { var _ = System.Math.Log(10); }
        public void Dep040() { var _ = System.Math.Sin(0); }

        // --- DateTime members ---
        public void Dep041() { var _ = System.DateTime.Now; }
        public void Dep042() { var _ = System.DateTime.UtcNow; }
        public void Dep043() { var _ = System.DateTime.Today; }
        public void Dep044() { var _ = System.DateTime.MinValue; }
        public void Dep045() { var _ = System.DateTime.MaxValue; }
        public void Dep046() { var _ = System.DateTime.Parse("2020-01-01"); }
        public void Dep047() { var d = System.DateTime.Now; var _ = d.AddDays(1); }
        public void Dep048() { var d = System.DateTime.Now; var _ = d.AddHours(1); }
        public void Dep049() { var d = System.DateTime.Now; var _ = d.ToString("o"); }
        public void Dep050() { var d = System.DateTime.Now; var _ = d.ToUniversalTime(); }

        // --- Guid members ---
        public void Dep051() { var _ = System.Guid.NewGuid(); }
        public void Dep052() { var _ = System.Guid.Empty; }
        public void Dep053() { var _ = System.Guid.Parse("00000000-0000-0000-0000-000000000000"); }

        // --- Int32/Convert methods ---
        public void Dep054() { var _ = int.Parse("1"); }
        public void Dep055() { int.TryParse("1", out var _); }
        public void Dep056() { var _ = System.Convert.ToInt32("1"); }
        public void Dep057() { var _ = System.Convert.ToDouble("1.0"); }
        public void Dep058() { var _ = System.Convert.ToBoolean("true"); }
        public void Dep059() { var _ = System.Convert.ToString(1); }
        public void Dep060() { var _ = System.Convert.ToBase64String(new byte[] { 1 }); }
        public void Dep061() { var _ = System.Convert.FromBase64String("AQ=="); }

        // --- Path methods ---
        public void Dep062() { var _ = System.IO.Path.Combine("a", "b"); }
        public void Dep063() { var _ = System.IO.Path.GetFileName("a/b.txt"); }
        public void Dep064() { var _ = System.IO.Path.GetDirectoryName("a/b.txt"); }
        public void Dep065() { var _ = System.IO.Path.GetExtension("a.txt"); }
        public void Dep066() { var _ = System.IO.Path.GetFileNameWithoutExtension("a.txt"); }
        public void Dep067() { var _ = System.IO.Path.GetTempPath(); }
        public void Dep068() { var _ = System.IO.Path.GetTempFileName(); }
        public void Dep069() { var _ = System.IO.Path.HasExtension("a.txt"); }
        public void Dep070() { var _ = System.IO.Path.IsPathRooted("/a"); }

        // --- StringBuilder methods ---
        public void Dep071() { var sb = new System.Text.StringBuilder(); sb.Append("a"); }
        public void Dep072() { var sb = new System.Text.StringBuilder(); sb.AppendLine("a"); }
        public void Dep073() { var sb = new System.Text.StringBuilder(); sb.Insert(0, "a"); }
        public void Dep074() { var sb = new System.Text.StringBuilder(); sb.Replace("a", "b"); }
        public void Dep075() { var sb = new System.Text.StringBuilder(); sb.Clear(); }
        public void Dep076() { var sb = new System.Text.StringBuilder(); var _ = sb.Length; }
        public void Dep077() { var sb = new System.Text.StringBuilder(); var _ = sb.Capacity; }

        // --- Environment members ---
        public void Dep078() { var _ = System.Environment.NewLine; }
        public void Dep079() { var _ = System.Environment.MachineName; }
        public void Dep080() { var _ = System.Environment.UserName; }
        public void Dep081() { var _ = System.Environment.CurrentDirectory; }
        public void Dep082() { var _ = System.Environment.OSVersion; }
        public void Dep083() { var _ = System.Environment.ProcessorCount; }
        public void Dep084() { var _ = System.Environment.TickCount; }
        public void Dep085() { var _ = System.Environment.GetEnvironmentVariable("PATH"); }

        // --- Array/List methods ---
        public void Dep086() { System.Array.Sort(new[] { 3, 1, 2 }); }
        public void Dep087() { System.Array.Reverse(new[] { 1, 2, 3 }); }
        public void Dep088() { var _ = System.Array.IndexOf(new[] { 1, 2 }, 1); }
        public void Dep089() { System.Array.Copy(new[] { 1 }, new int[1], 1); }
        public void Dep090() { var _ = new System.Collections.Generic.List<int>(); }
        public void Dep091() { var l = new System.Collections.Generic.List<string>(); l.Add("a"); }
        public void Dep092() { var l = new System.Collections.Generic.List<string>(); l.Contains("a"); }
        public void Dep093() { var l = new System.Collections.Generic.List<string>(); l.Remove("a"); }
        public void Dep094() { var l = new System.Collections.Generic.List<string>(); l.Sort(); }
        public void Dep095() { var d = new System.Collections.Generic.Dictionary<string, int>(); d.Add("a", 1); }

        // --- Type / object methods ---
        public void Dep096() { var _ = typeof(string).FullName; }
        public void Dep097() { var _ = typeof(int).Name; }
        public void Dep098() { var _ = typeof(object).Assembly; }
        public void Dep099() { var o = new object(); var _ = o.GetHashCode(); }
        public void Dep100() { var o = new object(); var _ = o.ToString(); }
        public void Dep101() { var o = new object(); var _ = o.GetType(); }
        public void Dep102() { var _ = object.ReferenceEquals(null, null); }

        // --- Additional distinct members to push past 105 ---
        public void Dep103() { var _ = System.IO.File.Exists("a.txt"); }
        public void Dep104() { var _ = System.IO.Directory.Exists("a"); }
        public void Dep105() { var _ = System.Text.Encoding.UTF8; }
        public void Dep106() { var _ = System.Text.Encoding.ASCII; }
        public void Dep107() { var _ = System.BitConverter.GetBytes(1); }
        public void Dep108() { var _ = System.GC.GetTotalMemory(false); }
        public void Dep109() { var _ = System.Threading.Thread.CurrentThread; }
        public void Dep110() { var _ = System.Diagnostics.Stopwatch.StartNew(); }
        public void Dep111() { var _ = System.IO.Path.GetFullPath("a"); }
        public void Dep112() { var _ = System.StringComparer.Ordinal; }
        public void Dep113() { var _ = System.StringComparer.OrdinalIgnoreCase; }
        public void Dep114() { var _ = System.Random.Shared; }
        public void Dep115() { var _ = System.Text.Encoding.Unicode; }
    }
}
