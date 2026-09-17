import { define } from "../utils.ts";

export default define.page(function App({ Component }) {
  return (
    <html>
      <head>
        <meta charset="utf-8" />
        <meta name="viewport" content="width=device-width, initial-scale=1.0" />
        <title>OpenS4League</title>
        <meta
          name="description"
          content="OpenS4League — an unofficial, modern rebuild of the S4 League server and tooling stack."
        />
        <link
          rel="icon"
          type="image/svg+xml"
          href="/logos/os4l-crest-flat-dark.svg"
        />
        <script src="/copy.js" defer></script>
      </head>
      <body>
        <Component />
      </body>
    </html>
  );
});
