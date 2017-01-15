﻿using UnityEngine;
using System.Collections;

public class BoxComponent : worldObject {
	
	public GameObject remains;
	public float kE_Requirement = 100;
	
	// Use this for initialization
	void Start () {
	}
	/*
	public static float KineticEnergy(Collision2D collision){
		// mass in kg, velocity in meters per second, result is
		return 0.5f*GameObject.Find("breakable_box").rigidbody2D.mass*Mathf.Pow(collision.relativeVelocity.magnitude,2);
	}*/
	
	void OnCollisionEnter2D(Collision2D collision) {
		bool destroyAll = false;
		foreach (HingeJoint2D h in gameObject.GetComponents<HingeJoint2D>())
		{
			float mag = h.GetReactionForce(1.0f).magnitude;
			Debug.Log(mag);
			if (mag>kE_Requirement)
			{
				destroyAll = true;
			}
		}
		if (destroyAll)
			foreach (HingeJoint2D h in gameObject.GetComponents<HingeJoint2D>())
				Destroy(h);
			
	/*	
		if (collision.collider.name != "apple") {
			double kE = KineticEnergy (collision);
			
			if (kE > kE_Requirement) {
				GameObject broken_box = (GameObject)Instantiate(remains, transform.position, transform.rotation);
				DestroyObject (gameObject);
			}
		}*/
		//if (collision.relativeVelocity > 10)
	}
	
	
	// Update is called once per frame
	void Update () {
		
	}
}
