using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Main : MonoBehaviour
{
	[SerializeField] RawImage pictureBox;
	[SerializeField] Camera mainCamera;
	[SerializeField] Transform[] objects; // 全て球
	[SerializeField] int screenWidth = 512;
	[SerializeField] int screenHeight = 512;
	[SerializeField] int raysPerFrame = 2048;
	[SerializeField] int maxReflection = 2;
	[SerializeField] bool startButton;

	Texture2D texture;

	void Start()
	{
		BeginRender();
	}

	void BeginRender()
	{
		texture = new Texture2D(screenWidth, screenHeight);
		pictureBox.texture = texture;
	}

	void Update()
	{
		if (startButton)
		{
			startButton = false;
			BeginRender();
		}
		
		Render();
	}

	void Render()
	{
		for (int i = 0; i < raysPerFrame; i++)
		{
			var x = Random.Range(0, screenWidth);
			var y = Random.Range(0, screenHeight);
			Render(x, y);
		}
		texture.Apply();
	}

	void Render(int x, int y) // スクリーン座標
	{
		var ray = CreateRay(x, y);
		Color color = new Color(0f, 0f, 0f, 1f); // 黒
		// 世界との交差判定を行い、規定回数反射する間にライトに当たれば白、当たらなければ黒を返す
		for (int i = 0; i < maxReflection; i++)
		{
			Ray reflectedRay;
			var hitObject = CastRay(ray, out reflectedRay);
			if (hitObject != null) // 何かに当たった
			{
				if (hitObject.gameObject.name == "Light") // それがライトなら色を出力して抜ける
				{
					color = new Color(1f, 1f, 1f, 1f);
					break;
				}
				else // 違うものに当たったので反射して継続
				{
					ray = reflectedRay;
				}
			}
			else // 何にも当たらなかった。抜ける。
			{
				break;
			}
		}
		texture.SetPixel(x, y, color);
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

	Ray CreateRay(int x, int y)
	{
		// 出発点はカメラのワールド座標
		Ray ray = new Ray();
		ray.origin = mainCamera.transform.position;
		// x,yは左下起点なので、使いやすいように中央起点に直す。それには解像度の半分を引けばいい
		var fx = x - (screenWidth * 0.5f); // floatにしたいので以下fxとする
		var fy = y - (screenHeight * 0.5f);
		// 半ピクセルずらす(幅16でx=8だったら、これは後半の最初のピクセルなので座標としては中心からは0.5ピクセルずれる)
		fx += 0.5f;
		fy += 0.5f;

		// スクリーンの位置を仮想的に確定する
		// 今fx,fyがそのまま座標として使える距離にスクリーンがあるとすると、
		// スクリーン縦幅の半分を、スクリーンまでの距離で割ったものが、tan(fieldOfView / 2)に相当する
		// (screenHeight/2) / z = tan(fieldOfView / 2) ここからDを求める
		var z = screenHeight * 0.5f / Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
		// これでレイが定まった
		// ただしこれはカメラのビュー座標なので、ワールド座標に変換する必要がある。回転クォタニオンを掛けても良いが簡単にやる。
		var dir = (mainCamera.transform.right * fx)
			+ (mainCamera.transform.up * fy) 
			+ (mainCamera.transform.forward * z);
		ray.direction = dir.normalized;

		return ray;
	}
}
