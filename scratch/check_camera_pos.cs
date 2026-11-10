var player = GameObject.Find("PlayerSpawnPoint");
if (player != null) Debug.Log($"PlayerSpawnPoint: {player.transform.position}");
var cam = Camera.main;
if (cam != null) Debug.Log($"Main Camera: {cam.transform.position}, Rot: {cam.transform.rotation.eulerAngles}");
var ter = Terrain.activeTerrain;
if (ter != null) {
    Vector3 c = Vector3.zero;
    c.y = ter.SampleHeight(c) + ter.transform.position.y;
    Debug.Log($"Terrain Center (0,0): {c}");
}

