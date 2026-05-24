#import "AVFoundation/AVFoundation.h"

@implementation AudioSessionSetter
 
extern "C" {
   void _SetAudioSession()
   {
       [[AVAudioSession sharedInstance] setCategory:AVAudioSessionCategoryPlayback error:nil];
       [[AVAudioSession sharedInstance] setActive:YES error:nil];
   }
}
 
@end