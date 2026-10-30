# Videos

The video lists used by the launcher's **Community** home panel and **MORE VIDEOS** catalog. Each folder has a
`videos.txt`, one video per line:
`address | title | channel | channel's address` (everything after the address is optional: a missing title and
channel are asked from YouTube when the video is first shown). Lines starting with `#` are notes.

- **big-streamers/**: videos used by the **Big Streamers** filter. They are embedded in the launcher at build time.
- **content-creators/**: videos used by the **Content Creators** filter. They are embedded in the launcher at build time.

The Guide's **Gameplay → Build Guides** list is maintained separately in `assets/guide.txt`. The launcher groups
those pages under class headings when it can identify a class from the page title, description or text; pages that
do not identify one appear under **Uncategorized**. Build Guide videos also remain part of the Community video pool.
