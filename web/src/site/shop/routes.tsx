import { Navigate, type RouteObject } from "react-router";

import { shopPaths } from "./paths";
import { ProductPage } from "./ProductPage";
import { ShowcasePage } from "./ShowcasePage";

/**
 * The shop's routes, under `/shop` (roadmap 16.4); the tests render the same table. Both pages
 * are open to anyone: nothing here is behind a sign-in, and none of the app's routes are here.
 */
export const shopRoutes: RouteObject[] = [
  { path: shopPaths.showcase, element: <ShowcasePage /> },
  { path: shopPaths.productPattern, element: <ProductPage /> },
  { path: "*", element: <Navigate to={shopPaths.showcase} replace /> },
];
