// The two native calls the game needs on iOS (ADR 0043 §9): the system's Reduce Motion setting, and a light haptic tap.
#import <UIKit/UIKit.h>

extern "C"
{
    bool ftc_reduce_motion()
    {
        return UIAccessibilityIsReduceMotionEnabled();
    }

    void ftc_haptic_light()
    {
        UIImpactFeedbackGenerator *tap = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight];
        [tap impactOccurred];
    }
}
