using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InfiniteTerrain : MonoBehaviour
{
 const float viewerMoveThresholdForChunkUpdate = 25f;
 const float sqrViewerMoveThresholdForChunkUpdate = viewerMoveThresholdForChunkUpdate * viewerMoveThresholdForChunkUpdate;
 const float colliderGenerationDstThreshold = 5;
 public LODInfo[] detailLevels;
 public static float maxViewDistance;
 public Transform viewer;
 public Material mapMaterial;
 public static Vector2 viewerPosition;
 Vector2 viewerPositionOld;
 static MapGenerator mapGenerator;
 float meshWorldSize;
 int chunksVisibleInViewDst;
 public int colliderLODIndex;
 Dictionary<Vector2, TerrainChunk> terrainChunkDictionary = new Dictionary<Vector2, TerrainChunk>();
 static List<TerrainChunk> visibleTerrainChunks = new List<TerrainChunk>();
 public GameObject waterPrefab;
 Prefabs prefabs;

 private void Start()
 {
  prefabs = FindObjectOfType<Prefabs>();
  mapGenerator = FindObjectOfType<MapGenerator>();
  maxViewDistance = detailLevels[detailLevels.Length - 1].visibleDstThreshold;
  meshWorldSize = mapGenerator.meshSettings.meshWorldSize;
  chunksVisibleInViewDst = Mathf.RoundToInt(maxViewDistance / meshWorldSize);
  UpdateVisibleChunks();
 }

 private void Update()
 {
  viewerPosition = new Vector2(viewer.position.x, viewer.position.z);

  if (viewerPosition != viewerPositionOld)
  {
   foreach (TerrainChunk chunk in visibleTerrainChunks)
   {
    chunk.UpdateCollisionMesh();
   }
  }

  if ((viewerPositionOld - viewerPosition).sqrMagnitude > sqrViewerMoveThresholdForChunkUpdate)
  {
   viewerPositionOld = viewerPosition;
   UpdateVisibleChunks();
  }
 }

 void UpdateVisibleChunks()
 {
  HashSet<Vector2> alreadyUpdatedChunkCoords = new HashSet<Vector2>();
  for (int i = visibleTerrainChunks.Count-1; i >= 0; i--)
  {
   alreadyUpdatedChunkCoords.Add(visibleTerrainChunks[i].coord);
   visibleTerrainChunks[i].UpdateTerrainChunk();
  }

  int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x / meshWorldSize);
  int currentChunkCoordY = Mathf.RoundToInt(viewerPosition.y / meshWorldSize);

  for (int yOffset = -chunksVisibleInViewDst; yOffset <= chunksVisibleInViewDst; yOffset++)
  {
   for (int xOffset = -chunksVisibleInViewDst; xOffset <= chunksVisibleInViewDst; xOffset++)
   {
    Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);
    if (!alreadyUpdatedChunkCoords.Contains(viewedChunkCoord))
    { 
     if (terrainChunkDictionary.ContainsKey(viewedChunkCoord))
     {
      terrainChunkDictionary[viewedChunkCoord].UpdateTerrainChunk();
     }
     else
     {
      terrainChunkDictionary.Add(viewedChunkCoord, new TerrainChunk(viewedChunkCoord, meshWorldSize, detailLevels, colliderLODIndex, transform, mapMaterial, waterPrefab, prefabs));
     }
    }
   }
  }
 }

 public class TerrainChunk
 {
  public Vector2 coord;
  Vector2 sampleCentre;
  GameObject meshObject;
  Bounds bounds;
  HeightMap mapData;
  MeshRenderer meshRenderer;
  MeshFilter meshFilter;
  MeshCollider meshCollider;
  LODInfo[] detailLevels;
  LODMesh[] lodMeshes;
  int colliderLODIndex;
  bool mapDataReceived;
  int previousLODIndex = -1;
  bool hasSetCollider;
  GameObject waterObject;
  Prefabs prefabs;
  List<GameObject> objectList = new List<GameObject>();

  public TerrainChunk(Vector2 coord, float meshWorldSize, LODInfo[] detailLevels, int colliderLODIndex, Transform parent, Material material, GameObject waterPrefab, Prefabs prefabs)
  {
   this.prefabs = prefabs;
   this.coord = coord;
   this.detailLevels = detailLevels;
   this.colliderLODIndex = colliderLODIndex;
   sampleCentre = coord * meshWorldSize / mapGenerator.meshSettings.meshScale;
   Vector2 position = coord * meshWorldSize;
   bounds = new Bounds(position, Vector2.one * meshWorldSize);
   meshObject = new GameObject("TerrainChunk");
   meshRenderer = meshObject.AddComponent<MeshRenderer>();
   meshFilter = meshObject.AddComponent<MeshFilter>();
   meshCollider = meshObject.AddComponent<MeshCollider>();
   meshRenderer.material = material;
   meshObject.transform.position = new Vector3(position.x, 0, position.y);
   meshObject.transform.parent = parent;
   SetVisible(false);

            //Vector3 waterPos = new Vector3(meshObject.transform.position.x, meshObject.transform.position.y + (6 * 5), meshObject.transform.position.z);
            //waterObject = new GameObject("Water");
            //waterObject = Instantiate(waterPrefab, waterPos, Quaternion.identity) as GameObject;
            //waterObject.transform.parent = meshObject.transform;
            //waterObject.transform.localScale = new Vector3(2.36f * 5, 0, 1.18f * 5);

   lodMeshes = new LODMesh[detailLevels.Length];

   for (int i = 0; i < detailLevels.Length; i++)
   {
    lodMeshes[i] = new LODMesh(detailLevels[i].lod);
    lodMeshes[i].updateCallback += UpdateTerrainChunk;

    if (i == colliderLODIndex)
    {
     lodMeshes[i].updateCallback += UpdateCollisionMesh;
    }
   }

   mapGenerator.RequestHeightMap(sampleCentre, OnMapDataReceived);
  }

  void OnMapDataReceived(HeightMap mapData)
  {
   this.mapData = mapData;
   mapDataReceived = true;
   UpdateTerrainChunk();
  }


  public void UpdateTerrainChunk()
  {
   if (mapDataReceived)
   {
    float viewerDstFromNearestEdge = Mathf.Sqrt(bounds.SqrDistance(viewerPosition));

    bool wasVisible = IsVisible(); 
    bool visible = viewerDstFromNearestEdge <= maxViewDistance;


    if (visible)
    {
     int lodIndex = 0;
     for (int i = 0; i < detailLevels.Length - 1; i++)
     {
      if (viewerDstFromNearestEdge > detailLevels[i].visibleDstThreshold)
      {
       lodIndex = i + 1;
      }
      else
      {
       break;
      }
     }

     if (lodIndex != 0)
     {
      for (int i = 0; i < objectList.Count; i++)
      {
       Destroy(objectList[i].gameObject);
      }
     }

     if (lodIndex != previousLODIndex)
     {
      LODMesh lodMesh = lodMeshes[lodIndex];
      if (lodMesh.hasMesh)
      {
       previousLODIndex = lodIndex;
       meshFilter.mesh = lodMesh.mesh;

       if (lodIndex == 0)
       {
        GameObject waterObject;
        Vector3 waterPosition = new Vector3(meshObject.transform.position.x, meshObject.transform.position.y + (6 * 5), meshObject.transform.position.z);
        waterObject = new GameObject("Water");
        waterObject = Instantiate(prefabs.water, waterPosition, Quaternion.identity) as GameObject;
        waterObject.transform.parent = meshObject.transform;
        waterObject.transform.localScale = new Vector3(2.36f * 5, 0, 1.18f * 5);
        objectList.Add(waterObject);
       }

       Vector3[] vertices = meshFilter.mesh.vertices;
       for (int i = 0; i < vertices.Length; i++)
       {
        // Handles the creation of assets under the water level
        if ((vertices[i].y > 0f) && (vertices[i].y < 20f) && (lodIndex == 0))
        {
         int prefabRandom = Random.Range(0, 100);
         Vector3 position = new Vector3(vertices[i].x + this.meshObject.transform.position.x, vertices[i].y + this.meshObject.transform.position.y, vertices[i].z + this.meshObject.transform.position.z);

         GameObject gameObject;
                               
         if (prefabRandom == 0)
         {
          int scaleRandom = Random.Range(15, 35);
          gameObject = Instantiate(prefabs.waterRock, position, Quaternion.identity) as GameObject;
          gameObject.transform.Rotate(-90, Random.Range(0, 100), 0);
          gameObject.transform.localScale = gameObject.transform.localScale / scaleRandom;
          objectList.Add(gameObject);
         }
         else if (prefabRandom == 1)
         {
          int scaleRandom = Random.Range(15, 35);
          gameObject = Instantiate(prefabs.waterRock2, position, Quaternion.identity) as GameObject;
          gameObject.transform.Rotate(-90, Random.Range(0, 100), 0);
          gameObject.transform.localScale = gameObject.transform.localScale / scaleRandom;
          objectList.Add(gameObject);
         }
         else if (prefabRandom == 2)
         {
          int scaleRandom = Random.Range(15, 35);
          gameObject = Instantiate(prefabs.waterRock3, position, Quaternion.identity) as GameObject;
          gameObject.transform.Rotate(-90, Random.Range(0, 100), 0);
          gameObject.transform.localScale = gameObject.transform.localScale / scaleRandom;
          objectList.Add(gameObject);
         }
         else if (prefabRandom == 3)
         {
          int scaleRandom = Random.Range(15, 35);
          gameObject = Instantiate(prefabs.waterRock4, position, Quaternion.identity) as GameObject;
          gameObject.transform.Rotate(-90, Random.Range(0, 100), 0);
          gameObject.transform.localScale = gameObject.transform.localScale / scaleRandom;
          objectList.Add(gameObject);
         }
         else if (prefabRandom >= 4 && prefabRandom <= 10)
         {
          int scaleRandom = Random.Range(1, 5);
          gameObject = Instantiate(prefabs.waterElement, position, Quaternion.identity) as GameObject;
          gameObject.transform.Rotate(0, Random.Range(0, 100), 0);
          gameObject.transform.localScale = gameObject.transform.localScale * scaleRandom;
          objectList.Add(gameObject);
         }
        }

        // Handles the creation of assets at the water edge
        if ((vertices[i].y > 25f) && (vertices[i].y < 35f) && (lodIndex == 0))
        {
         int prefabRandom = Random.Range(0, 50);
         Vector3 position = new Vector3(vertices[i].x + this.meshObject.transform.position.x, vertices[i].y + this.meshObject.transform.position.y, vertices[i].z + this.meshObject.transform.position.z);

         GameObject gameObject;

         if (prefabRandom == 0)
         {
          int scaleRandom = Random.Range(1, 5);
          gameObject = Instantiate(prefabs.waterEdge, position, Quaternion.identity) as GameObject;
          gameObject.transform.Rotate(0, Random.Range(0, 100), 0);
          gameObject.transform.localScale = gameObject.transform.localScale * scaleRandom;
          objectList.Add(gameObject);
         }
         else if (prefabRandom == 1)
         {
          int scaleRandom = Random.Range(1, 5);
          gameObject = Instantiate(prefabs.waterElement, position, Quaternion.identity) as GameObject;
          gameObject.transform.Rotate(0, Random.Range(0, 100), 0);
          gameObject.transform.localScale = gameObject.transform.localScale * scaleRandom;
          objectList.Add(gameObject);
         }
         else if (prefabRandom == 2)
         {
          int scaleRandom = Random.Range(1, 5);
          gameObject = Instantiate(prefabs.waterEdge2, position, Quaternion.identity) as GameObject;
          gameObject.transform.Rotate(0, Random.Range(0, 100), 0);
          gameObject.transform.localScale = gameObject.transform.localScale * scaleRandom;
          objectList.Add(gameObject);
         }
        }

        // Handles asset creation on grass areas
        if ((vertices[i].y > 35f) && (vertices[i].y < 40f) && (lodIndex == 0))
        {
         int prefabRandom = Random.Range(1, 50);
         Vector3 position = new Vector3(vertices[i].x + this.meshObject.transform.position.x, vertices[i].y + this.meshObject.transform.position.y, vertices[i].z + this.meshObject.transform.position.z);

         GameObject gameObject;

         if (prefabRandom == 0)
         {
          int scaleRandom = Random.Range(1, 5);
          gameObject = Instantiate(prefabs.grassLevel, position, Quaternion.identity) as GameObject;
          gameObject.transform.Rotate(0, Random.Range(0, 100), 0);
          gameObject.transform.localScale = gameObject.transform.localScale * scaleRandom;
          objectList.Add(gameObject);
         }
         else if (prefabRandom == 1)
         {
          int scaleRandom = Random.Range(1, 5);
          gameObject = Instantiate(prefabs.grassLevel2, position, Quaternion.identity) as GameObject;
          gameObject.transform.Rotate(0, Random.Range(0, 100), 0);
          gameObject.transform.localScale = gameObject.transform.localScale * scaleRandom;
          objectList.Add(gameObject);
         }
         else if (prefabRandom == 2)
         {
          int scaleRandom = Random.Range(1, 5);
          gameObject = Instantiate(prefabs.grassLevel3, position, Quaternion.identity) as GameObject;
          gameObject.transform.Rotate(0, Random.Range(0, 100), 0);
          gameObject.transform.localScale = gameObject.transform.localScale * scaleRandom;
          objectList.Add(gameObject);
         }
         else if (prefabRandom == 3)
         {
          int scaleRandom = Random.Range(1, 5);
          gameObject = Instantiate(prefabs.grassLevel4, position, Quaternion.identity) as GameObject;
          gameObject.transform.Rotate(0, Random.Range(0, 100), 0);
          gameObject.transform.localScale = gameObject.transform.localScale * scaleRandom;
          objectList.Add(gameObject);
         }
         else if (prefabRandom == 4)
         {
          int scaleRandom = Random.Range(1, 5);
          gameObject = Instantiate(prefabs.grassLevel5, position, Quaternion.identity) as GameObject;
          gameObject.transform.Rotate(0, Random.Range(0, 100), 0);
          gameObject.transform.localScale = gameObject.transform.localScale * scaleRandom;
          objectList.Add(gameObject);
         }
         else if (prefabRandom == 5)
         {
          int scaleRandom = Random.Range(1, 5);
          gameObject = Instantiate(prefabs.grassLevel6, position, Quaternion.identity) as GameObject;
          gameObject.transform.Rotate(0, Random.Range(0, 100), 0);
          gameObject.transform.localScale = gameObject.transform.localScale * scaleRandom;
          objectList.Add(gameObject);
         }
         else if (prefabRandom == 6)
         {
          int scaleRandom = Random.Range(1, 5);
          gameObject = Instantiate(prefabs.grassLevel7, position, Quaternion.identity) as GameObject;
          gameObject.transform.Rotate(0, Random.Range(0, 100), 0);
          gameObject.transform.localScale = gameObject.transform.localScale * scaleRandom;
          objectList.Add(gameObject);
         }
         else if (prefabRandom > 6 && prefabRandom < 30)
         {
          int scaleRandom = Random.Range(1, 5);
          gameObject = Instantiate(prefabs.grassLevel8, position, Quaternion.identity) as GameObject;
          gameObject.transform.Rotate(0, Random.Range(0, 100), 0);
          gameObject.transform.localScale = gameObject.transform.localScale * scaleRandom;
          objectList.Add(gameObject);
         }

        }





        // Handles asset creation on the mountain regions
        if ((vertices[i].y > 45f) && (vertices[i].y < 70f) && (lodIndex == 0))
        {
         int prefabRandom = Random.Range(0, 30);
         Vector3 position = new Vector3(vertices[i].x + this.meshObject.transform.position.x, vertices[i].y + this.meshObject.transform.position.y, vertices[i].z + this.meshObject.transform.position.z);

         GameObject gameObject;

         if (prefabRandom == 0)
         {
          int scaleRandom = Random.Range(5, 20);
          gameObject = Instantiate(prefabs.rockLevel, position, Quaternion.identity) as GameObject;
          gameObject.transform.Rotate(0, Random.Range(0, 100), 0);
          gameObject.transform.localScale = gameObject.transform.localScale / scaleRandom;
          objectList.Add(gameObject);
         }


        }








       }



      }
      else if (!lodMesh.hasRequestedMesh)
      {
       lodMesh.RequestMesh(mapData);
      }
     }
                }

    if (wasVisible != visible)
    {
     if (visible)
     {
      visibleTerrainChunks.Add(this);
     }
     else
     {
      visibleTerrainChunks.Remove(this);
      for (int i = 0; i < objectList.Count; i++)
      {
       Destroy(objectList[i].gameObject);
      }
     }
     SetVisible(visible);
    }
   }
  }

  public void UpdateCollisionMesh()
  {
   if (!hasSetCollider)
   {
    float sqrDstFromViewerToEdge = bounds.SqrDistance(viewerPosition);

    if (sqrDstFromViewerToEdge < detailLevels[colliderLODIndex].sqrVisibleDstThreshold)
    {
     if (!lodMeshes[colliderLODIndex].hasRequestedMesh)
     {
      lodMeshes[colliderLODIndex].RequestMesh(mapData);
     }
    }

    if (sqrDstFromViewerToEdge < (colliderGenerationDstThreshold * colliderGenerationDstThreshold))
    {
     if (lodMeshes[colliderLODIndex].hasMesh)
     {
      meshCollider.sharedMesh = lodMeshes[colliderLODIndex].mesh;
      hasSetCollider = true;
     }
    }
   }
  }

  public void SetVisible(bool visible)
  {
   meshObject.SetActive(visible);
  }

  public bool IsVisible()
  {
   return meshObject.activeSelf;
  }
 }

 class LODMesh
 {
  public Mesh mesh;
  public bool hasRequestedMesh;
  public bool hasMesh;
  int lod;
  public event System.Action updateCallback;

  public LODMesh(int lod)
  {
   this.lod = lod;
  }

  void OnMeshDataReceived(MeshData meshData)
  {
   mesh = meshData.CreateMesh();
   hasMesh = true;
   updateCallback();
  }

  public void RequestMesh(HeightMap mapData)
  {
   hasRequestedMesh = true;
   mapGenerator.RequestMeshData(mapData, lod, OnMeshDataReceived);
  }
 }

 [System.Serializable]
 public struct LODInfo
 {
  [Range(0, MeshSettings.numSupportedLODs-1)]
  public int lod;
  public float visibleDstThreshold;
  public float sqrVisibleDstThreshold
  {
   get
   {
    return visibleDstThreshold * visibleDstThreshold;
   }
  }
 }
}
