-- Lays out the Earmatch DMG window in Finder: icon view without toolbar, the drawn background, and the app and
-- the Applications link placed on the wells of scripts/dmg-background.swift. Finder saves it all in the
-- volume's .DS_Store, which is what people see when they open the DMG.
--
-- Usage: osascript scripts/dmg-layout.applescript MOUNT_POINT
-- MOUNT_POINT is a read-write mount of the image; Finder must be able to see it (attach without -nobrowse).
-- The disk is addressed by its path, so another mounted volume with the same name does not get in the way.

on run argv
	set mountPoint to item 1 of argv
	set diskAlias to (POSIX file mountPoint) as alias
	set backgroundAlias to (POSIX file (mountPoint & "/.background/background.tiff")) as alias
	tell application "Finder"
		set theDisk to item diskAlias
		open theDisk
		set theWindow to container window of theDisk
		set current view of theWindow to icon view
		set toolbar visible of theWindow to false
		set statusbar visible of theWindow to false
		set pathbar visible of theWindow to false
		-- 640 x 400 content, the size of the background. Finder's bounds include the title bar.
		set bounds of theWindow to {200, 120, 840, 548}
		set viewOptions to icon view options of theWindow
		set arrangement of viewOptions to not arranged
		set icon size of viewOptions to 128
		set text size of viewOptions to 13
		set label position of viewOptions to bottom
		set shows item info of viewOptions to false
		set shows icon preview of viewOptions to false
		set background picture of viewOptions to backgroundAlias
		set position of item "Earmatch.app" of theDisk to {160, 200}
		set position of item "Applications" of theDisk to {480, 200}
		close theWindow
		open theDisk
		update theDisk without registering applications
		delay 2
		close container window of theDisk
	end tell
end run
