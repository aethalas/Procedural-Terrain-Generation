using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu]
public class MeshSettings : UpdateableData
{
 public float meshScale = 1f;
 public bool useFlatShading;
 public const int numSupportedLODs = 5;
 public const int numSupportedChunkSizes = 9;
 public const int numSupportedFlatShadedChunkSizes = 3;
 public static readonly int[] supportedChunkSizes = { 48, 72, 96, 120, 144, 168, 192, 216, 240 };
 [Range(0, numSupportedChunkSizes - 1)]
 public int chunkSizeIndex;
 [Range(0, numSupportedFlatShadedChunkSizes - 1)]
 public int flatShadedChunkSizeIndex;

 // num verts per line of mesh rendered at LOD = 0
 // Includes two extra verts that are removed from the final mesh generated.
 // Used for calculating normals.
 public int numVertsPerLine
 {
  get
  {
   return supportedChunkSizes[(useFlatShading) ? flatShadedChunkSizeIndex : chunkSizeIndex] + 1;
  }
 }

 public float meshWorldSize
 {
  get
  {
   return (numVertsPerLine - 3) * meshScale;
  }
 }
}
