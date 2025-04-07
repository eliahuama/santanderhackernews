# HackerNews Best Stories API

This ASP.NET Core API fetches the top `n` best stories from Hacker News by score.

## Run

```bash
dotnet run
```

## Choose top n stories
```bash
curl http://localhost:{port}/api/stories/top-{n}
```