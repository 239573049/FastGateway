import { AuthorizeInput } from "../module";
import {  postJson } from "../utils/fetch";

export const authorize = async (data: AuthorizeInput) => {
    return postJson('/api/v1/Authorize', data);
}