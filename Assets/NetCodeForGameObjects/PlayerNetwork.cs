using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerNetwork : NetworkBehaviour
{
    private NetworkVariable<int> randomValue = new NetworkVariable<int>(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public override void  OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        randomValue.OnValueChanged += (oldValue, newValue) =>
        {
            Debug.Log($"{OwnerClientId} Random value changed from {oldValue} to {newValue}");
        };
        Debug.Log("OnNetworkSpawn");
    }
    // Update is called once per frame
    void Update()
    {
        if (!IsOwner)
            return;

        if (Input.GetKey(KeyCode.T)) randomValue.Value = Random.Range(0, 100);

        Vector3 moveDir = new Vector3();
        if (Input.GetKey(KeyCode.W)) moveDir.y += 1;
        if (Input.GetKey(KeyCode.S)) moveDir.y -= 1;
        if (Input.GetKey(KeyCode.A)) moveDir.x -= 1;
        if (Input.GetKey(KeyCode.D)) moveDir.x += 1;

        float moveSpeed = 3f;
        transform.position = transform.position + moveDir * (moveSpeed * Time.deltaTime);
    }
}
