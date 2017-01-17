using UnityEngine;
using System.Collections.Generic;

public class DeathWall : MonoBehaviour {

    private List<int> stopLayers;

	// Use this for initialization
	void Awake () {
        stopLayers = new List<int>() { LayerMask.NameToLayer("Body"),
            LayerMask.NameToLayer("Hands") };
	}
	
	// Update is called once per frame
	void Update () {
	
	}

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!stopLayers.Contains(other.gameObject.layer))
            Destroy(other.gameObject);

        else
            other.GetComponent<Rigidbody2D>().velocity = new Vector2(0, 0);
    }
}
