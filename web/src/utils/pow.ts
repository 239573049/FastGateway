import { get } from "@/utils/fetch";

interface PowMessage {
  solution?: string;
  attempts?: number;
  error?: string;
}

let workerUrl: Promise<string> | null = null;

/** 求解器脚本由后端下发，与网关验证页共用同一份实现 */
function loadWorkerUrl(): Promise<string> {
  workerUrl ??= get("/api/v1/authorization/pow-worker.js")
    .then((script: Blob) => URL.createObjectURL(new Blob([script], { type: "text/javascript" })))
    .catch((error: unknown) => {
      workerUrl = null;
      throw error;
    });
  return workerUrl;
}

/** 在 Web Worker 中求解 PoW，返回满足难度的 n */
export async function solvePow(
  challenge: string,
  difficulty: number,
  onProgress?: (attempts: number) => void
): Promise<string> {
  const url = await loadWorkerUrl();

  return new Promise((resolve, reject) => {
    const worker = new Worker(url);
    worker.onmessage = (event: MessageEvent<PowMessage>) => {
      const data = event.data;
      if (data.solution !== undefined) {
        worker.terminate();
        resolve(data.solution);
      } else if (data.error) {
        worker.terminate();
        reject(new Error(data.error));
      } else if (data.attempts) {
        onProgress?.(data.attempts);
      }
    };
    worker.onerror = (event) => {
      worker.terminate();
      reject(new Error(event.message));
    };
    worker.postMessage({ challenge, difficulty });
  });
}
