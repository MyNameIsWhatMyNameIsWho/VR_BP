using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShelfOfTargetsSideAnim : NetworkBehaviour
{
    [SerializeField] private List<Target> targets;
    [SerializeField] private Transform leftEnd, rightEnd, topEnd, bottomEnd;
    [SerializeField] private float speedVertical, speedHorizontal;

    void Update()
    {
        if (!isServer) return;
        foreach (var target in targets)
        {
            var tpos = target.transform.position;

            if (target.isGoingRight)
            {
                tpos += new Vector3(Time.deltaTime * speedHorizontal, 0.0f, 0.0f);                
                if (tpos.x >= rightEnd.position.x) { 
                    target.isGoingRight = false;
                    target.canStandUpAnim = false;
                    target.Fall();
                }
            } else
            {
                tpos -= new Vector3(Time.deltaTime * speedHorizontal, 0.0f, 0.0f);
                if (tpos.x <= leftEnd.position.x) { 
                    target.isGoingRight = true;
                    target.canStandUpAnim = true;
                    target.StandUp();
                }
            }
            SetTargetPosition(target, tpos);

            if (!target.standing) continue;

            if (target.isGoingUp)
            {
                tpos += new Vector3(0.0f, Time.deltaTime * speedVertical, 0.0f);
                if (tpos.y >= topEnd.position.y) target.isGoingUp = false;
            } else
            {
                tpos -= new Vector3(0.0f, Time.deltaTime * speedVertical, 0.0f);
                if (tpos.y <= bottomEnd.position.y) target.isGoingUp = true;
            }
            SetTargetPosition(target, tpos);
        }
    }

    private void SetTargetPosition(Target target, Vector3 tpos)
    {
        target.transform.position = tpos;
    }
}
