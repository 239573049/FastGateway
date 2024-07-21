function formatDate(value: string) {
    var date = new Date(value);
    const padZero = (num: number) => (num < 10 ? '0' : '') + num;

    const year = date.getFullYear();
    const month = padZero(date.getMonth() + 1); // getMonth() 返回值范围是 0-11，所以需要加 1
    const day = padZero(date.getDate());
    const hours = padZero(date.getHours());
    const minutes = padZero(date.getMinutes());

    return `${year}-${month}-${day} ${hours}:${minutes}`;
}

/// 生成13位时间戳
function getTimestamp() {
    return new Date().getTime();
}

export {
    formatDate,
    getTimestamp
}

