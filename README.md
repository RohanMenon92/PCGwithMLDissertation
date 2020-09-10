# Machine Learning for Terrain Generation Dissertation
This is my thesis project for an MSc in Computer Games Technology from Abertay University.

In the project, a noise algorithm is used to create procedural terrain meshes that can be set by a designer. A machine learning(ML) agent is trained to improve the noise parameters that are used for this terrain. 
The concept of using an ML agent to improve PCG showcased here can be used to complement the work of designers or to set a baseline that the designers can then modify. 

The dissertation: https://github.com/RohanMenon92/PCGwithMLDissertation/blob/master/Dissertation_1901120.pdf
Video: https://youtu.be/02UyNq3En4I

Shader Graph is used to create height based, slope based terrain and also physical water waves.



## Scenes
- Generator: Creates infinite terrain, alow for playing a 3rd Person game or a top down point and click game
- GeneratorTrainer: Used to train a ML agent to understand good terrain generation and use inference from the Neural Networks in Assets/Brains

## Neural Networks
The video describes different neural networks trained and how they learn differently from one another

The dissertation project compares different learning methods, how the agent should interact with the terrain and how different curriculum strategies affect the agent.

The end result of the project is many different neural networks that have interesting ways of solving what is defined as ideal terrain using the given noise values.

## Screenshots
![Screen](https://github.com/RohanMenon92/PCGwithMLDissertation/blob/master/Screenshots/HeightMapSetting.PNG)
![Shield2](https://github.com/RohanMenon92/PCGwithMLDissertation/blob/master/Screenshots/MeshCreator.PNG)
![Gameplay](https://github.com/RohanMenon92/PCGwithMLDissertation/blob/master/Screenshots/PoissonDistributor.PNG)
![Gameplay](https://github.com/RohanMenon92/PCGwithMLDissertation/blob/master/Screenshots/PoissonDistributorScript.PNG)
![Gameplay](https://github.com/RohanMenon92/PCGwithMLDissertation/blob/master/Screenshots/RunTimeNavMesh.PNG)
![Gameplay](https://github.com/RohanMenon92/PCGwithMLDissertation/blob/master/Screenshots/ThirdPersonGame.PNG)