// @ts-ignore
window._env_ = window._env_ || {};

export async function stream() {
    // @ts-ignore
    const response = await fetch(window._env_.api_url + "/api/gateway/network", {
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
