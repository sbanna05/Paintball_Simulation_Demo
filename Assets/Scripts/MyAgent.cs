using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class MyAgent : Agent
{

    // This will control how fast the submarine will go
    public float speed = 5f;

    // This is a reference to the target (chest)
    public Transform TargetTransform;

    // Since we are only dealing with discrete actions,
    // we can simply use an enum to enumerate all of the
    // possible actions
    private enum ACTIONS
    {
        LEFT = 0,
        FORWARD = 1,
        RIGHT = 2,
        BACKWARD = 3
    }

    // OnEpisodeBegin() gets called once at the start
    // of a new episode. In other words, when we reset
    // the environment.
    public override void OnEpisodeBegin()
    {
        // We reset the submarine
        transform.localPosition = new Vector3(0, 0.5f, 0);

        // And we reset the chest as well
        TargetTransform.localPosition = new Vector3(5, 0.5f, 5);
    }

    // The CollectObservations() function supplies all of the
    // relevant information to the agent. You can think of this
    // function as the "eyes" of the agent.
    public override void CollectObservations(VectorSensor sensor)
    {
        // Submarine positions
        sensor.AddObservation(transform.localPosition.x);
        sensor.AddObservation(transform.localPosition.z);

        // Chest positions
        sensor.AddObservation(TargetTransform.localPosition.x);
        sensor.AddObservation(TargetTransform.localPosition.z);

    }

    // OnActionReceived() executes the actions chosen by the agent
    public override void OnActionReceived(ActionBuffers actions)
    {
        // We select the first branch
        var actionTaken = actions.DiscreteActions[0];

        // Then we simply check what the agent chose and rotate the
        // agent accordingly
        switch (actionTaken)
        {
            case (int)ACTIONS.FORWARD:
                transform.rotation = Quaternion.Euler(0, 0, 0);
                break;
            case (int)ACTIONS.LEFT:
                transform.rotation = Quaternion.Euler(0, -90, 0);
                break;
            case (int)ACTIONS.RIGHT:
                transform.rotation = Quaternion.Euler(0, 90, 0);
                break;
            case (int)ACTIONS.BACKWARD:
                transform.rotation = Quaternion.Euler(0, 180, 0);
                break;
        }

        // Finally, we also move the agent forward
        transform.Translate(Vector3.forward * speed * Time.fixedDeltaTime);

        // And penalize the agent for each timestep
        AddReward(-0.01f);
    }


    // The Heuristic() function is optional. However, it is still
    // really important to debug our agents.
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var actions = actionsOut.DiscreteActions;

        // The GetAxisRaw() function returns values from the [-1,1]
        // interval
        var horizontal = Input.GetAxisRaw("Horizontal");
        var vertical = Input.GetAxisRaw("Vertical");

        if (horizontal == -1)
        {
            actions[0] = (int)ACTIONS.LEFT;
        }
        else if (horizontal == +1)
        {
            actions[0] = (int)ACTIONS.RIGHT;
        }
        else if (vertical == -1)
        {
            actions[0] = (int)ACTIONS.BACKWARD;
        }
        else if (vertical == +1)
        {
            actions[0] = (int)ACTIONS.FORWARD;
        }
        else
        {
            actions[0] = (int)ACTIONS.FORWARD;
        }
    }

    // The OnCollisionEnter() function is called when a collision
    // between our GameObject (the submarine) and another object
    // happens
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.tag == "Wall")
        {
            // If the agent hit a wall, we want to penalize it
            // and end the episode
            AddReward(-1);
            EndEpisode();
        }
        else if (collision.collider.tag == "Treasure")
        {
            // If the agent found the treasure, then we reward it
            // and end the episode
            AddReward(+1);
            EndEpisode();
        }
    }
}
