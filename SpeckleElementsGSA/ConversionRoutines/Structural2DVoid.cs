﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using SpeckleCore;
using SpeckleCoreGeometryClasses;
using SpeckleElements;

namespace SpeckleElementsGSA
{
  [GSAObject("MEMB.7", new string[] { "NODE.2" }, "elements", false, true, new Type[] { typeof(GSANode) }, new Type[] { })]
  public class GSA2DVoid : IGSASpeckleContainer
  {
    public string GWACommand { get; set; }
    public List<string> SubGWACommand { get; set; } = new List<string>();
    public dynamic Value { get; set; } = new Structural2DVoid();

    public void ParseGWACommand(GSAInterfacer GSA, List<GSANode> nodes)
    {
      if (this.GWACommand == null)
        return;

      Structural2DVoid obj = new Structural2DVoid();

      string[] pieces = this.GWACommand.ListSplit(",");

      int counter = 1; // Skip identifier
      obj.StructuralId = pieces[counter++];
      obj.Name = pieces[counter++].Trim(new char[] { '"' });
      var color = pieces[counter++].ParseGSAColor();

      counter++; // Type
      counter++; // Property
      counter++; // Group

      List<double> coordinates = new List<double>();
      string[] nodeRefs = pieces[counter++].ListSplit(" ");
      for (int i = 0; i < nodeRefs.Length; i++)
      {
        GSANode node = nodes.Where(n => n.Value.StructuralId == nodeRefs[i]).FirstOrDefault();
        coordinates.AddRange(node.Value);
        this.SubGWACommand.Add(node.GWACommand);
      }

      Structural2DVoid temp = new Structural2DVoid(
          coordinates.ToArray(),
          color.HexToArgbColor());

      obj.Vertices = temp.Vertices;
      obj.Faces = temp.Faces;
      obj.Colors = temp.Colors;

      this.Value = obj;
    }

    public void SetGWACommand(GSAInterfacer GSA)
    {
      if (this.Value == null)
        return;

      Structural2DVoid v = this.Value as Structural2DVoid;

      string keyword = typeof(GSA2DVoid).GetGSAKeyword();

      int index = GSA.Indexer.ResolveIndex(typeof(GSA2DVoid), v);

      List<string> ls = new List<string>();

      ls.Add("SET");
      ls.Add(keyword + ":" + GSA.GenerateSID(v));
      ls.Add(index.ToString());
      ls.Add(v.Name == null || v.Name == "" ? " " : v.Name);
      ls.Add(v.Colors == null || v.Colors.Count() < 1 ? "NO_RGB" : v.Colors[0].ArgbToHexColor().ToString());
      ls.Add("2D_VOID_CUTTER");
      ls.Add("1"); // Property reference
      ls.Add("0"); // Group
      string topo = "";
      List<int[]> connectivities = v.Edges();
      List<double> coor = new List<double>();
      foreach (int[] conn in connectivities)
        foreach (int c in conn)
        {
          coor.AddRange(v.Vertices.Skip(c * 3).Take(3));
          topo += GSA.NodeAt(v.Vertices[c * 3], v.Vertices[c * 3 + 1], v.Vertices[c * 3 + 2], Conversions.GSACoincidentNodeAllowance).ToString() + " ";
        }
      ls.Add(topo);
      ls.Add("0"); // Orientation node
      ls.Add("0"); // Angles
      ls.Add("1"); // Target mesh size
      ls.Add("MESH"); // TODO: What is this?
      ls.Add("LINEAR"); // Element type
      ls.Add("0"); // Fire
      ls.Add("0"); // Time 1
      ls.Add("0"); // Time 2
      ls.Add("0"); // Time 3
      ls.Add("0"); // TODO: What is this?
      ls.Add("ACTIVE"); // Dummy
      ls.Add("NO"); // Internal auto offset
      ls.Add("0"); // Offset z
      ls.Add("ALL"); // Exposure

      GSA.RunGWACommand(string.Join("\t", ls));
    }
  }

  public static partial class Conversions
  {
    public static bool ToNative(this Structural2DVoid v)
    {
      new GSA2DVoid() { Value = v }.SetGWACommand(GSA);

      return true;
    }

    public static SpeckleObject ToSpeckle(this GSA2DVoid dummyObject)
    {
      if (!GSASenderObjects.ContainsKey(typeof(GSA2DVoid)))
        GSASenderObjects[typeof(GSA2DVoid)] = new List<object>();

      List<GSA2DVoid> voids = new List<GSA2DVoid>();
      List<GSANode> nodes = GSASenderObjects[typeof(GSANode)].Cast<GSANode>().ToList();

      string keyword = typeof(GSA2DVoid).GetGSAKeyword();
      string[] subKeywords = typeof(GSA2DVoid).GetSubGSAKeyword();

      string[] lines = GSA.GetGWARecords("GET_ALL," + keyword);
      List<string> deletedLines = GSA.GetDeletedGWARecords("GET_ALL," + keyword).ToList();
      foreach (string k in subKeywords)
        deletedLines.AddRange(GSA.GetDeletedGWARecords("GET_ALL," + k));

      // Remove deleted lines
      GSASenderObjects[typeof(GSA2DVoid)].RemoveAll(l => deletedLines.Contains((l as IGSASpeckleContainer).GWACommand));
      foreach (KeyValuePair<Type, List<object>> kvp in GSASenderObjects)
        kvp.Value.RemoveAll(l => (l as IGSASpeckleContainer).SubGWACommand.Any(x => deletedLines.Contains(x)));

      // Filter only new lines
      string[] prevLines = GSASenderObjects[typeof(GSA2DVoid)].Select(l => (l as IGSASpeckleContainer).GWACommand).ToArray();
      string[] newLines = lines.Where(l => !prevLines.Contains(l)).ToArray();

      foreach (string p in newLines)
      {
        string[] pPieces = p.ListSplit(",");
        if (pPieces[4].MemberIs2D())
        {
          // Check if void
          if (pPieces[4] == "2D_VOID_CUTTER")
          {
            GSA2DVoid v = new GSA2DVoid() { GWACommand = p };
            v.ParseGWACommand(GSA, nodes);
            voids.Add(v);
          }
        }
      }

      GSASenderObjects[typeof(GSA2DVoid)].AddRange(voids);

      if (voids.Count() > 0 || deletedLines.Count() > 0) return new SpeckleObject();

      return new SpeckleNull();
    }
  }
}
