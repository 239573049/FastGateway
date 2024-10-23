

import { post } from "@/utils/fetch"

const baseUrl = "/api/v1/authorization";


const Auth = (password: string) => {
    return post(baseUrl + "?password=" + password);
}

export {
    Auth
}