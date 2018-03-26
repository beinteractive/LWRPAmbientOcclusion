# Ambient Occlusion for Lightweight Render Pipeline

![main_camera_2018_0320_161657](https://user-images.githubusercontent.com/1482297/37642184-913e7de2-2c5f-11e8-9ba4-8fdb221db713.gif)

## Background

As a Lightweight Render Pipeline design decision, Post-Processing Stack v2 Ambient Occlusion does not work in LWRP. (Because it uses compute shader)

So I made a custom post-processing stack v2 effect to force rendering AO.

## How to use

Add `Custom/LWRP Ambient Occlusion` to your post-processing profile.

<img width="322" alt="2018-03-20 16 38 02" src="https://user-images.githubusercontent.com/1482297/37642189-9621cbc0-2c5f-11e8-8467-6fa869038927.png">

And configure built-in AO.

<img width="378" alt="2018-03-20 16 38 23" src="https://user-images.githubusercontent.com/1482297/37642191-96475e26-2c5f-11e8-9818-9ac87e0be149.png">

## Limitation

 - It only works for MSVO.
