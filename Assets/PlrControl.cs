using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlrControl : MonoBehaviour
{
    public Rigidbody Rigidbody;
    public Camera Camera;
    float maxSpeed = 5;
    float yaw = 0;
    float pitch = 0;
    bool mouseLook = true;
    Vector3 mousePrevious;
    void Start()
    {
        mousePrevious = Input.mousePosition;
    }
    
    void Update()
    {
        
        Vector3 currentPos = Input.mousePosition;
        if (mouseLook)
        {
            yaw += currentPos.x - mousePrevious.x;
            pitch -= (currentPos.y - mousePrevious.y)*.3f;
        }
        mousePrevious = Input.mousePosition;
    }

    void FixedUpdate()
    {
        if (Input.GetKey(KeyCode.W))
        {
            Rigidbody.AddForce(transform.forward * 5000 * Time.deltaTime);
        }
        if (Input.GetKey(KeyCode.S))
        {
            Rigidbody.AddForce(-transform.forward * 5000 * Time.deltaTime);
        }
        if (Input.GetKey(KeyCode.D))
        {
            Rigidbody.AddForce(transform.right * 5000 * Time.deltaTime);
        }
        if (Input.GetKey(KeyCode.A))
        {
            Rigidbody.AddForce(-transform.right * 5000 * Time.deltaTime);
        }
        if(Input.GetKeyDown(KeyCode.M)) { mouseLook = !mouseLook; }

        Rigidbody.angularVelocity = Vector3.zero;
        GetComponent<Rigidbody>().rotation = Quaternion.Euler(0, yaw, 0);
        Camera.transform.localRotation = Quaternion.Euler(pitch, yaw, 0);
        Camera.transform.position = transform.position + new Vector3(0, transform.localScale.y/2f - .1f, 0);


        if (Rigidbody.velocity.magnitude > maxSpeed)
        {
            Rigidbody.velocity = Rigidbody.velocity.normalized * maxSpeed;
        }
    }
}
