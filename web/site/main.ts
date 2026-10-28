// The site app's entry, inside the site's Vite root (site/) so the dev server can serve it at
// /main.ts. A path out of the root in index.html (../src/site/main.tsx) builds, but in
// development the browser resolves it against the page's address and asks for /src/…, which is
// not there. An import from here is resolved by Vite on disk, in both. The app is src/site/.
import "../src/site/main";
