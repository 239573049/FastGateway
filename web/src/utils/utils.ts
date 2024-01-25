export function debounce<T extends (...args: any[]) => any>(func: T, wait: number, immediate: boolean = false): (...args: Parameters<T>) => void {
    let timeout: number | null = null;

    return function(...args: Parameters<T>) {
        // @ts-ignore
        const context = this;

        const later = () => {
            timeout = null;
            if (!immediate) func.apply(context, args);
        };

        const callNow = immediate && !timeout;
        if (timeout !== null) {
            clearTimeout(timeout);
        }
        timeout = window.setTimeout(later, wait);

        if (callNow) func.apply(context, args);
    };
}