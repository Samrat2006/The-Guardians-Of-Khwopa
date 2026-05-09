using UnityEngine;

public class PlayerReset : MonoBehaviour
{
    void Start()
    {
        transform.position = new Vector3(0, 1, 0); // your spawn position
        transform.rotation = Quaternion.identity;
    }
}