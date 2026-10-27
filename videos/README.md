# Videos

The YouTube videos the launcher shows, sorted in two groups. Each folder has a `videos.txt`, one video per line:
`address | title | channel | channel's address` (everything after the address is optional: a missing title and
channel are asked from YouTube when the video is first shown). Lines starting with `#` are notes.

- **big-streamers/**: videos by big streamers. They are the featured videos on the launcher's front page while the
  date in `videos.txt` has not passed, and they always stay first in MORE VIDEOS, in this order. The launcher carries
  this list inside itself (rebuild after a change).
- **content-creators/**: every other video, the players' build guides among them. They are sorted here for later
  use; for now the launcher shows them from the Guide's Build Guides (assets/guide.txt).
