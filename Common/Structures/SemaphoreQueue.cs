using System.Collections.Concurrent;

namespace vut_ipk2.Common.Structures;

public class SemaphoreQueue
{
    private SemaphoreSlim semaphore;
    private ConcurrentQueue<TaskCompletionSource<bool>> queue = new();
    
    public SemaphoreQueue(int initialCount, int maxCount)
    {
        semaphore = new SemaphoreSlim(initialCount, maxCount);
    }
    
    public Task WaitAsync()
    {
        var tcs = new TaskCompletionSource<bool>();
        queue.Enqueue(tcs);
        semaphore.WaitAsync().ContinueWith(t =>
            {
                if (queue.TryDequeue(out var popped))
                    popped.SetResult(true);
            }
        );
        return tcs.Task;
    }
    
    public void Release()
    {
        semaphore.Release();
    }
}