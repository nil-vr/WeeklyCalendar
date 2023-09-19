This repository contains a nil.weekly-calendar Unity package for regular weekly event calendars. The data is loaded over the internet so it stays up to date without needing to rebuild the world.

The data for events is not contained within this repository.

This calendar is based on the static event calendar created by [@Makoto479].

[@Makoto479]: https://twitter.com/Makoto479

# Installation

It's recommended that you install this package using [VCC].

1. Add my package repository my clicking this link: [https://nil-vr.github.io/vpm/install.html](https://nil-vr.github.io/vpm/install.html)
2. When managing your project in VCC, "Weekly Calendar" will appear as an installable package.
3. After installing the package, in Unity, under Packages/Weekly Calendar/Samples there will be two prefabs that can be dragged into your world.

[VCC]: https://vcc.docs.vrchat.com/

# Creating your own calendar data

To deal with VRChat limitations and keep the calendar event data editable by event organizers, the [wc-compiler] tool converts multiple [toml] files into a single [json] file, and organizes images into a directory that can be loaded by the calendar object.

This conversion can be done automatically by [GitHub Actions], and you can see any example of this in the [wc-undou] repository (the `.github` folder). If you're using GitHub, the only thing you need to do is [enable GitHub Pages using the GitHub Actions source][enable-pages] and place the same `.github` folder in your repository. On every push to the main branch, the GitHub Pages will update. Your calendar will be at `https://YOU.github.io/REPO/data.json`.

You'll also want to copy the `events/meta.toml` file to the same location in your repository and edit it. This file is the only file from the `events` directory that is required.

[wc-compiler]: https://github.com/nil-vr/wc-compiler
[toml]: https://toml.io/
[json]: https://www.json.org/
[GitHub Actions]: https://docs.github.com/actions
[wc-undou]: https://github.com/nil-vr/wc-undou
[enable-pages]: https://docs.github.com/pages/getting-started-with-github-pages/configuring-a-publishing-source-for-your-github-pages-site#publishing-with-a-custom-github-actions-workflow

# Tips

## Customizations

The U# component on the calendar has many customization options. Some of these options are references to child objects of the calendar. As long as you keep these references in tact, you can edit the child objects as well for additional customization. However, if you make too many customizations to the child objects, it may become difficult to upgrade to new versions of the calendar later.
