public interface IAmbientAnim
{
    void PlayIdle();
    void PlayWalk(float speed01);
    void PlayPose(SpotPose pose);   // Sit, Look, Vendor, Talk, etc.
    void ClearPose();
}
