import { API_URL } from "../store/config";

export async function stream() {
    // @ts-ignore
    if (API_URL && API_URL.endsWith("/")) {
        // @ts-ignore
        API_URL = API_URL.substring(0, API_URL.length - 1);
    }
    // @ts-ignore
    const response = await fetch(API_URL + "/api/gateway/system", {
        method: "GET",
        headers: {
            "Authorization": "Bearer " + localStorage.getItem("access_token")
        }
    });

    if (response.status === 401) {
        localStorage.removeItem("access_token");
        window.location.href = "/login";
        return Promise.reject("error");
    }

    const reader = response.body!.getReader();

    return {
        [Symbol.asyncIterator]() {
            return {
                async next() {
                    const { done, value } = await reader.read();
                    if (done) {
                        return { done: true, value: null };
                    }
                    let text = new TextDecoder("utf-8").decode(value);

                    let lines = text.split('\n');

                    for (let i = 0; i < lines.length; i++) {
                        let line = lines[i].substring("data:".length);
                        return {
                            done: false,
                            value: JSON.parse(line),
                        };
                    }
                },
            };
        },
    };
}
