# Animation Rigging Playground
Just a sandbox for me to experiment with Unity's animation rigging package:
https://blogs.unity3d.com/2019/05/14/introducing-the-animation-rigging-preview-package-for-unity-2019-1/

## Rig Setups
<img src="https://i.imgur.com/5EGcmK3.gif" width=49% /> <img src="https://i.imgur.com/rJGyg9Q.gif" width=49% /> <img src="https://i.imgur.com/JvfvqUz.gif" width=49% /> <img src="https://i.imgur.com/S4yFNG3.gif" width=49% /> <img src="https://i.imgur.com/P7K3J2l.gif" width=49% /> <img src="https://i.imgur.com/arzj0zo.gif" width=49% /> <img src="https://i.imgur.com/4P5CRRk.gif" width=49% />

## Custom Constraints
I currently have a basic constraint that allows you to remap a transform from Source object to Destination object.
For example, you could transform a translation in X of a source object from 0-1 (rack) to a rotation in Z of 0-360 (pinion).
![Remap Transform Constraint Inspector](https://i.imgur.com/bxSCelh.png)

Added a constraint that handles a 6DOF robotic arm, adapted from [Meuse Robotics](https://meuse.co.jp/?p=885). (Needs some cleanup, probably)

## In the Pipe
Currently working on a simplistic RBF solver. This GIF shows a monobehaviour prototype, not an actual constraint [yet].
<img src="https://i.imgur.com/aB1VqGX.gif" />

## Todo
