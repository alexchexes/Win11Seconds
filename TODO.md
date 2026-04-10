- ~~Win-ARM64 native support~~
- Improve resizing behavior.
  - ~~Top and bottom edges should allow resizing, and it must work properly in the current locked-aspect-ratio sense.~~
  - Probably can allow unlocking aspect-ratio (while still keeping nice little paddings when needed)
- ~~Support Windows transparency effects~~
- Custom colors support?
- Allow exiting full screen with ESC
- Allow hiding with ESC when not in full screen
- Add AM/PM and other format support with auto-detection of the current Windows language/region settings
- Option to show small date in the corner in fullscreen mode; ON by default
- Native minimize/enlarge/close window controls? We added a custom cross for a reason and removed the default Windows top window bar to make the whole window a monolithic surface without differentiated areas; we should not break that.
- Add auto start-up ON/OFF option via context menu, instead of the current manual way described in the README.
- ~~Always-on-top toggle? Now it is always on top unconditionally~~

While implementing changes should aim to keep executable size and background RAM usage minimal.
