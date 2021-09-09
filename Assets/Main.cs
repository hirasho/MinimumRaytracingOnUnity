using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Main : MonoBehaviour
{
	[SerializeField] RawImage pictureBox;
	[SerializeField] Camera mainCamera;
	[SerializeField] Transform[] objects; // 全て球
	[SerializeField] Text rayCountText;

	[SerializeField] int screenWidth = 512;
	[SerializeField] int screenHeight = 512;
	[SerializeField] int raysPerFrame = 2048;
	[SerializeField] int maxReflection = 2;
	[SerializeField] float exposure = 1f;
	[SerializeField] bool startButton;

	Texture2D texture;
	double rayCount;
	Color[] accumulatedPixels;
	Color[] normalizedPixels;

	void Start()
	{
		BeginRender();
	}

	void BeginRender()
	{
		accumulatedPixels = new Color[screenWidth * screenHeight];
		normalizedPixels = new Color[accumulatedPixels.Length];
		texture = new Texture2D(screenWidth, screenHeight);
		pictureBox.texture = texture;
		rayCount = 0.0;
		for (int i = 0; i < accumulatedPixels.Length; i++)
		{
			accumulatedPixels[i] = new Color(0f, 0f, 0f, 1f);
		}
	}

	void Update()
	{
		if (startButton)
		{
			startButton = false;
			BeginRender();
		}
		
		Render();
		rayCountText.text = "RAYS: " + rayCount;
	}

	void Render()
	{
		for (int i = 0; i < raysPerFrame; i++)
		{
			var x = Random.Range(0, screenWidth);
			var y = Random.Range(0, screenHeight);
			Sample();
		}
		float normalizedFactor = (screenWidth * screenHeight) / (float)rayCount * exposure;
		for (int i = 0; i < accumulatedPixels.Length; i++)
		{
			normalizedPixels[i] = accumulatedPixels[i] * normalizedFactor;
		}
		texture.SetPixels(normalizedPixels);
		texture.Apply();
	}

	void Sample() 
	{
		int x, y;
		var ray = GenerateRay(out x, out y);
		Color color = new Color(0f, 0f, 0f, 1f); // 黒
		Color albedo = new Color(1f, 1f, 1f, 1f); // 白
		// 世界との交差判定を行い、規定回数反射する間にライトに当たれば白、当たらなければ黒を返す
		for (int i = 0; i < maxReflection; i++)
		{
			Ray reflectedRay;
			var hitObject = CastRay(ray, out reflectedRay);
			if (hitObject != null) // 何かに当たった
			{
				var renderer = hitObject.gameObject.GetComponent<Renderer>();
				if (renderer != null)
				{
					var material = renderer.sharedMaterial;
					var materialAlbedo = material.GetColor("_Color");
					var materialEmission = material.GetColor("_EmissionColor");
					// こいつが光っている値は、ここまでのalbedoを乗じて加算
					color += materialEmission * albedo;
					// albedoを更新
					albedo *= materialAlbedo;
				}
				ray = reflectedRay;
			}
			else // 何にも当たらなかった。抜ける。
			{
				break;
			}
		}
		accumulatedPixels[(y * screenWidth) + x] += color;
		rayCount += 1.0;
	}

	// 何かに当たれば当たったobjectのtransformを返し、reflectedRayに反射rayを返す
	Transform CastRay(Ray ray, out Ray reflectedRay)
	{
		reflectedRay = new Ray();
		Transform ret = null;
		float minHitTime = float.MaxValue;
		for (int i = 0; i < objects.Length; i++)
		{
			float hitTime;
			Vector3 hitPosition;
			if (TestIntersection(ray, objects[i].position, objects[i].localScale.x * 0.5f, out hitTime, out hitPosition))
			{
				if (hitTime < minHitTime)
				{
					minHitTime = hitTime;
					// 反射レイを計算する。単純にランダム
					reflectedRay.origin = hitPosition;
					Vector3 d;
					d.x = Random.Range(-1f, 1f);
					d.y = Random.Range(-1f, 1f);
					d.z = Random.Range(-1f, 1f);
					d.Normalize();
					reflectedRay.direction = d;

					ret = objects[i];
				}
			}
		}
		return ret;
	}

	bool TestIntersection(
		Ray ray, 
		Vector3 sphereCenter, 
		float sphereRadius,
		out float hitTime,
		out Vector3 hitPosition)
	{
		hitTime = float.MaxValue;
		hitPosition = Vector3.zero;
		/* 
		直線と球の交差判定

		直線 A + Bt
		球中心 C
		として、
		A + Bt - C
		の長さが半径丁度になるtが存在すれば当たる。

		|A + Bt - C|の自身との内積が半径の二乗になれば良い

		A - C = Dとして、
		Dot(D + Bt, D + Bt) - r^2 = 0
		Dot(D,D) + 2*Dot(D,B)*t + Dot(B,B)*t^2 - r^2 = 0
		という二次方程式を解く
		*/
		var d = ray.origin - sphereCenter; // D
		var dd = Vector3.Dot(d, d);
		var db = Vector3.Dot(d, ray.direction);
		var bb = Vector3.Dot(ray.direction, ray.direction);
		// 二次方程式係数
		var ret = false;
		float t0, t1;
		if (SolveQuadratic(
			a2: bb, 
			a1: 2f * db, 
			a0: dd - (sphereRadius * sphereRadius),
			out t0,
			out t1))
		{
			// 時刻が負ならもう当たらないので、最小の正のものを探す
			// ただし、あんまりすぐ当たるのは演算誤差を考慮して外す
			if (t0 < 0.01f)
			{
				if (t1 < 0.01f)
				{
					// 解なし
				}			
				else
				{
					hitTime = t1;
					ret = true;
				}	
			}
			else
			{
				if (t1 < 0.01f)
				{
					hitTime = t0;
					ret = true;
				}
				else
				{
					hitTime = Mathf.Min(t0, t1);
					ret = true;
				}
			}

			// 当たっていれば位置を計算する
			if (ret)
			{
				hitPosition = ray.origin + (ray.direction * hitTime);
			}
		}
		return ret;
	}

	// 二次方程式を解く。実数解がなければfalseを返す。
	bool SolveQuadratic(float a2, float a1, float a0, out float x0, out float x1)
	{
		x0 = x1 = float.MaxValue;
		// 解の公式で解くが、解が虚数になるケースを除くために判別式を求める
		var discriminant = (a1 * a1) - (4f * a2 * a0);
		var ret = false;
		if (discriminant >= 0f)
		{
			// 2つの解を求める
			x0 = (-a1 - Mathf.Sqrt(discriminant)) / (2f * a2);
			x1 = (-a1 + Mathf.Sqrt(discriminant)) / (2f * a2);
			ret = true;
		}
		return ret;
	}

	// ランダムなレイを生成する。x,yにはスクリーン座標を返す
	Ray GenerateRay(out int xOut, out int yOut)
	{
		var screenPosition = new Vector2(Random.value, Random.value); // [0, 1]
		screenPosition *= new Vector2(screenWidth, screenHeight); //([0,w], [0,h])
		xOut = Mathf.Min((int)screenPosition.x, screenWidth - 1);
		yOut = Mathf.Min((int)screenPosition.y, screenHeight - 1);

		screenPosition -= new Vector2(screenWidth, screenHeight) * 0.5f; // 半分ずらす
		// スクリーンとカメラの距離zをfieldOfViewから計算する
		// tan(fieldOfView / 2) = (screenHeight/2) / z より
		var z = screenHeight * 0.5f / Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
		// レイが定まったがこれはビュー座標系なのでワールドに変換する
		var dir = mainCamera.transform.TransformVector(new Vector3(screenPosition.x, screenPosition.y, z));

		var ray = new Ray();
		ray.origin = mainCamera.transform.position;
		ray.direction = dir;
		return ray;
	}
}
