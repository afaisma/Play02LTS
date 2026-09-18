// Native physical footprint, the number jetsam actually applies the ~2 GB per-app limit to.
// Unity's Profiler counters (total/mono/gfx) are useful but none of them is the figure in the
// EXC_RESOURCE report, so the memory probe reads phys_footprint straight from the kernel.
// Returns bytes, or -1 if task_info fails. Called from MemProbe.Line via [DllImport("__Internal")].
#include <mach/mach.h>

extern "C" long long rb_phys_footprint()
{
    task_vm_info_data_t info;
    mach_msg_type_number_t count = TASK_VM_INFO_COUNT;
    kern_return_t kr = task_info(mach_task_self(), TASK_VM_INFO, (task_info_t)&info, &count);
    if (kr != KERN_SUCCESS) return -1;
    return (long long)info.phys_footprint;
}
