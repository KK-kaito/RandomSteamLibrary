日本語紹介＆詳細は[こちら](https://ponkotuelf-d.hatenablog.com/entry/2023/02/12/165259)

# RandomSteamLibrary

Randomised display of games owned by Steam.

# Features

- The following information is displayed at random from the games owned.
  - Title
  - Logo image
  - AppID
- Randomly displays only unplayed games from the list of owned games.
- Games you do not want to display can be excluded.
- Randomly displays games in your wishlist.
  - A json file must be placed.

# Usage

Set the following in the Config file.
- "SteamWebAPIkey"
- "SteamId"

To use the random display functionality of the wishlist, follow the steps below to place the json file.
(Because it could not be obtained if the wishlist was private.)
1. Open the Steam wishlist page and view the page source.
1. Search for "g_rgWishlistData", copy everything to the right of "=" and save as a json file.
   - ex.  [{"appid":4000,"priority":0,"added":1450940468}, ... ];
1. Located on the same level as "RandomSteamLibrary.exe".

# License

RandomSteamLibrary is under [MIT license](https://en.wikipedia.org/wiki/MIT_License).
