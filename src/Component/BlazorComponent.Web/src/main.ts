import * as interop from "./interop";
import * as overlayable from "./mixins/overlayable";

declare global {
  interface Window {
    BlazorComponent: any;
  }
}

window.BlazorComponent = {
  interop: {
    ...interop,
    ...overlayable,
  },
};