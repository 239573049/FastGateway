let API_URL : string = "";

// @ts-ignore
window._env_ = window._env_ || {};

// @ts-ignore
if(window._env_.api_url){
    // @ts-ignore
    API_URL = window._env_.api_url;
}

export  {
    API_URL
}