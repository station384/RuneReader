--- 
Package requirements
* packages needed on CachyOs to build OpenCV libs
sudo pacman -S --needed base-devel cmake ninja git pkgconf patchelf \
gtk3 ffmpeg libjpeg-turbo libpng libtiff 

* zlib is needed but CachyOs uses zlib-ng-compat which is equivalent.

* gtk3 is only needed if you build highgui (window display). If you don’t, you can drop it.
ffmpeg enables video codecs for videoio if you want it.


* Get the lib
``` bash
mkdir -p ~/build/opencv && cd ~/build/opencv
git clone --depth 1 --branch 4.10.0 https://github.com/opencv/opencv.git
mkdir -p opencv/build && cd opencv/build
```

* make the lib
  - WITH_FFMPEG only if you want video (ffmpeg packaged required)
  - WITH_GTK is used for window displays (gnome) this is used if you use cv2.show etc
  - WITH_QT is usd for window displays (kde or other QT deployments) this is used if you use cv2.show etc
  - WITH_TESSERACT is used if you use the OCR functionality (tesseract package required)
  - 
``` bash
cmake -G Ninja \
  -D CMAKE_BUILD_TYPE=Release \
  -D CMAKE_INSTALL_PREFIX=$HOME/.local/opencv-min \
  -D BUILD_SHARED_LIBS=ON \
  -D BUILD_TESTS=OFF \
  -D BUILD_PERF_TESTS=OFF \
  -D BUILD_EXAMPLES=OFF \
  -D BUILD_opencv_apps=OFF \
  -D BUILD_DOCS=OFF \
  -D OPENCV_ENABLE_NONFREE=OFF \
  -D WITH_TESSERACT=OFF \
  -D WITH_IPP=OFF \
  -D WITH_OPENCL=OFF \
  -D WITH_GTK=OFF \
  -D WITH_QT=OFF \
  -D WITH_V4L=ON \
  -D WITH_FFMPEG=OFF \
  -D WITH_GSTREAMER=OFF \
  -D BUILD_opencv_apps=OFF \
  -D BUILD_LIST=core,imgproc \
  ..
ninja
ninja install
```

Original build list
```
Old build option
- D BUILD_LIST=core,imgproc,imgcodecs,videoio,objdetect \
```

After build, you should have something like:
OpenCvSharpExtern/build/OpenCvSharpExtern.so (or libOpenCvSharpExtern.so depending on their CMake)

* Bundle the native libs into your app output

Create a native folder in your project (you can commit these outputs or copy during publish):
``` bash
APP_NATIVE=~/src/RuneReader/runtimes/linux-x64/native
mkdir -p "$APP_NATIVE"
```

Copy you're extern:
``` bash
cp -v ~/build/opencvsharp/src/OpenCvSharpExtern/build/*.so "$APP_NATIVE/"
```

Copy the OpenCV libs it needs:
``` bash
cp -v $HOME/.local/opencv-min/lib/libopencv_*.so* "$APP_NATIVE/"
```

* Not sure if this is needed.  
 Make the loader always use your bundled libs (RPATH = $ORIGIN)

This is the magic that avoids LD_LIBRARY_PATH.

Set RPATH on OpenCvSharpExtern.so:
``` bash
patchelf --set-rpath '$ORIGIN' "$APP_NATIVE/OpenCvSharpExtern.so" 2>/dev/null || true
patchelf --set-rpath '$ORIGIN' "$APP_NATIVE/libOpenCvSharpExtern.so" 2>/dev/null || true
```

Now verify what it needs:
``` bash
ldd "$APP_NATIVE/OpenCvSharpExtern.so" | grep -E "not found|tesseract|opencv"
```

You want:
* no “not found”
* no tesseract
* it should resolve libopencv_*.so from the same folder once deployed


Publish with RID so the runtimes/linux-x64/native folder is preserved
``` bash
   dotnet publish -c Release -r linux-x64
```

Then run from the publish directory and confirm it loads your bundled libs:
``` bash 
ldd ./runtimes/linux-x64/native/OpenCvSharpExtern.so | grep tesseract
```
Should output nothing.

--- 
Rider tip (so debug runs match publish) 

Rider sometimes runs from bin/Debug/... and not your publish folder. To keep behavior consistent:

Add this to your .csproj so your runtimes/linux-x64/native is always copied to output:
```
<ItemGroup>
  <None Include="runtimes/linux-x64/native/**" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```


WOW Game issues under linux:
Under KDE ctrl-F1 .. ctrl-F4 are mapped to kde short cuts and will interfer with the game
it is advised to change them to meta-F1 .. meta-F2 to allow for the hotkey desktop switching in kde and not
interfer with the game.

you can do this in KDE settings -> keyboard -> Shortcuts -> Window Manager
can change the switch to desktop mappings.

if you do not want to do this,  do not assign those keys to an action bar
and don't assign spells to them



