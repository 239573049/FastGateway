import { useCallback, useEffect, useRef, useState } from 'react';
import { ChevronsRight, Check, RefreshCw, X } from 'lucide-react';
import {
    getSliderChallenge,
    verifySlider,
    type SliderChallenge,
    type SliderTrackPoint,
} from '@/services/AuthorizationService';
import { cn } from '@/lib/utils';

type Status = 'loading' | 'idle' | 'dragging' | 'verifying' | 'success' | 'failed' | 'error';

/** 与服务端上限保持一致，超出部分不再记录 */
const MAX_TRACK_POINTS = 500;

interface SliderCaptchaProps {
    /** 验证通过时回传一次性票据；刷新或失败时回传 null。需要重新验证时由父组件更换 key 重建 */
    onTicket: (ticket: string | null) => void;
    disabled?: boolean;
}

const SliderCaptcha = ({ onTicket, disabled }: SliderCaptchaProps) => {
    const [challenge, setChallenge] = useState<SliderChallenge | null>(null);
    const [status, setStatus] = useState<Status>('loading');
    const [errorText, setErrorText] = useState('');
    const [offset, setOffset] = useState(0);
    const [scale, setScale] = useState(1);
    const boxRef = useRef<HTMLDivElement>(null);
    // 最新位移放在 ref 里：松手时 state 可能还没随最后一次 move 更新
    const drag = useRef<{
        startX: number;
        startY: number;
        startTime: number;
        offset: number;
        track: SliderTrackPoint[];
    } | null>(null);
    const onTicketRef = useRef(onTicket);
    onTicketRef.current = onTicket;

    const load = useCallback(async () => {
        setStatus('loading');
        setOffset(0);
        onTicketRef.current(null);
        try {
            const response = await getSliderChallenge();
            if (!response.success) throw new Error(response.message);
            setChallenge(response.data);
            setStatus('idle');
        } catch (e) {
            // 来源被锁定或取图过于频繁时服务端会给出等待提示
            setErrorText(e instanceof Error && e.message ? e.message : '验证图片加载失败');
            setChallenge(null);
            setStatus('error');
        }
    }, []);

    useEffect(() => {
        void load();
    }, [load]);

    // 图片按容器宽度缩放，拖动位移统一换算回背景图像素再提交
    useEffect(() => {
        const box = boxRef.current;
        if (!box || !challenge) return;
        const update = () => setScale(box.clientWidth / challenge.width);
        update();
        const observer = new ResizeObserver(update);
        observer.observe(box);
        return () => observer.disconnect();
    }, [challenge]);

    const maxOffset = challenge ? challenge.width - challenge.pieceSize : 0;
    const interactive = !disabled && challenge !== null && (status === 'idle' || status === 'dragging');

    const handlePointerDown = (e: React.PointerEvent<HTMLButtonElement>) => {
        if (!interactive || status !== 'idle') return;
        e.currentTarget.setPointerCapture(e.pointerId);
        drag.current = {
            startX: e.clientX,
            startY: e.clientY,
            startTime: e.timeStamp,
            offset: 0,
            track: [{ x: 0, y: 0, t: 0 }],
        };
        setStatus('dragging');
    };

    const handlePointerMove = (e: React.PointerEvent<HTMLButtonElement>) => {
        const current = drag.current;
        if (!current || status !== 'dragging') return;

        // 浏览器每帧只派发一次 move，合并事件里保留了帧间的真实采样点，轨迹更接近原始手势
        const samples = e.nativeEvent.getCoalescedEvents?.() ?? [];
        for (const sample of samples.length > 0 ? samples : [e.nativeEvent]) {
            const next = Math.min(Math.max((sample.clientX - current.startX) / scale, 0), maxOffset);
            current.offset = next;
            if (current.track.length < MAX_TRACK_POINTS) {
                current.track.push({
                    x: Math.round(next),
                    y: Math.round((sample.clientY - current.startY) / scale),
                    t: Math.max(0, Math.round(sample.timeStamp - current.startTime)),
                });
            }
        }
        setOffset(current.offset);
    };

    const handlePointerUp = async () => {
        const current = drag.current;
        drag.current = null;
        if (!current || !challenge || status !== 'dragging') return;

        const x = Math.round(current.offset);
        const last = current.track[current.track.length - 1];
        if (last.x !== x) current.track.push({ x, y: last.y, t: Math.round(performance.now() - current.startTime) });

        setStatus('verifying');
        try {
            const response = await verifySlider(challenge.id, x, current.track);
            if (response.success && typeof response.data === 'string') {
                setStatus('success');
                onTicketRef.current(response.data);
                return;
            }
        } catch {
            // 网络错误与验证失败一样处理：换一张图重来
        }
        setStatus('failed');
        window.setTimeout(() => void load(), 800);
    };

    const pieceLeft = offset * scale;
    const handleWidth = challenge ? challenge.pieceSize * scale : 44;

    return (
        <div className="w-full max-w-[300px] select-none space-y-2">
            <div
                ref={boxRef}
                className="relative w-full overflow-hidden rounded-lg border border-slate-200/70 bg-slate-100 dark:border-white/10 dark:bg-white/5"
                style={{ aspectRatio: challenge ? `${challenge.width} / ${challenge.height}` : '300 / 160' }}
            >
                {challenge ? (
                    <>
                        <img
                            src={challenge.background}
                            alt="滑块验证背景"
                            draggable={false}
                            className="absolute inset-0 h-full w-full"
                        />
                        <img
                            src={challenge.piece}
                            alt=""
                            draggable={false}
                            className="absolute top-0 h-full drop-shadow-[0_0_3px_rgba(0,0,0,0.55)]"
                            style={{ left: pieceLeft, width: handleWidth }}
                        />
                    </>
                ) : null}

                {status === 'loading' ? (
                    <div className="absolute inset-0 flex items-center justify-center text-sm text-muted-foreground">
                        正在加载验证图片…
                    </div>
                ) : null}

                {status === 'error' ? (
                    <button
                        type="button"
                        onClick={() => void load()}
                        className="absolute inset-0 flex items-center justify-center gap-2 text-sm text-muted-foreground hover:text-foreground"
                    >
                        <RefreshCw className="h-4 w-4" />
                        {errorText}，点击重试
                    </button>
                ) : null}

                {status === 'success' || status === 'failed' ? (
                    <div
                        className={cn(
                            'absolute inset-x-0 bottom-0 py-1.5 text-center text-xs font-medium text-white',
                            status === 'success' ? 'bg-emerald-600/90' : 'bg-red-600/90'
                        )}
                    >
                        {status === 'success' ? '验证通过' : '验证未通过，请重试'}
                    </div>
                ) : null}

                {challenge && status !== 'success' ? (
                    <button
                        type="button"
                        onClick={() => void load()}
                        disabled={disabled || status === 'verifying' || status === 'dragging'}
                        className="absolute right-1.5 top-1.5 rounded-md bg-black/35 p-1.5 text-white hover:bg-black/50 disabled:opacity-50"
                        aria-label="换一张"
                    >
                        <RefreshCw className="h-3.5 w-3.5" />
                    </button>
                ) : null}
            </div>

            <div className="relative h-10 w-full overflow-hidden rounded-lg border border-slate-200/70 bg-slate-100 dark:border-white/10 dark:bg-white/5">
                <div
                    className={cn(
                        'absolute inset-y-0 left-0',
                        status === 'success'
                            ? 'bg-emerald-500/20'
                            : status === 'failed'
                              ? 'bg-red-500/20'
                              : 'bg-blue-500/15'
                    )}
                    style={{ width: pieceLeft + handleWidth / 2 }}
                />
                <div className="pointer-events-none absolute inset-0 flex items-center justify-center text-xs text-muted-foreground">
                    {status === 'success' ? '' : status === 'verifying' ? '正在验证…' : '向右拖动滑块完成拼图'}
                </div>
                <button
                    type="button"
                    aria-label="拖动滑块"
                    disabled={!interactive}
                    onPointerDown={handlePointerDown}
                    onPointerMove={handlePointerMove}
                    onPointerUp={() => void handlePointerUp()}
                    onPointerCancel={() => void handlePointerUp()}
                    className={cn(
                        'absolute inset-y-0 flex touch-none items-center justify-center rounded-lg text-white shadow-sm transition-colors',
                        status === 'success'
                            ? 'bg-emerald-600'
                            : status === 'failed'
                              ? 'bg-red-600'
                              : 'bg-blue-600 hover:bg-blue-700 disabled:bg-slate-400 dark:disabled:bg-slate-600',
                        interactive ? 'cursor-grab active:cursor-grabbing' : 'cursor-default'
                    )}
                    style={{ left: pieceLeft, width: handleWidth }}
                >
                    {status === 'success' ? (
                        <Check className="h-4 w-4" />
                    ) : status === 'failed' ? (
                        <X className="h-4 w-4" />
                    ) : (
                        <ChevronsRight className="h-4 w-4" />
                    )}
                </button>
            </div>
        </div>
    );
};

export default SliderCaptcha;
