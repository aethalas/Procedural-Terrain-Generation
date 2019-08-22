using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu]
public class HeightMapSettings : UpdateableData
{
 public NoiseSettings noiseSettings;

 public AnimationCurve heightCurve;
 public float heightMultiplier;
 public bool useFalloff;
 public float minHeight
 {
  get
  {
   return heightMultiplier * heightCurve.Evaluate(0);
  }
 }
 public float maxHeight
 {
  get
  {
   return heightMultiplier * heightCurve.Evaluate(1);
  }
 }

#if UNITY_EDITOR
 protected override void OnValidate()
 {
  noiseSettings.ValidateValues();
  base.OnValidate();
 }
#endif
}
